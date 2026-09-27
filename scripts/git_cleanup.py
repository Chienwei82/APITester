#!/usr/bin/env python3
"""
Automatiza la limpieza y actualización de ramas locales después de mergear
un PR desde la web.

Qué hace:
1. git fetch --prune <remote>
2. Si la rama por defecto (main/master) local está detrás de origin y es
   fast-forward, la actualiza (solo si estamos parados en ella).
3. Detecta ramas locales cuyo tracking remoto [origin/xxx] ya no existe
   (es decir, fueron borradas al mergear el PR) y las elimina con `-d`
   (cortés: nunca borra algo sin mergear). Si la rama tiene un worktree
   adjunto, primero lo elimina.
4. Si `.gitignore` quedó modificado por el setup de un worktree previo
   (única diferencia: bloque `# Git worktrees` con `.worktrees/`), lo revierte.

Seguridad:
- Nunca borra la rama actual ni la rama por defecto.
- Nunca usa `-D`: un branch sin mergear se reporta y se deja intacto.
- `--ff-only` / fetch de main: si diverge, no fuerza nada, solo reporta.
- Nada de esto se ejecuta sin confirmación (salvo `--yes`).

Uso:
    python3 scripts/git_cleanup.py [--dry-run]
    python3 scripts/git_cleanup.py [--yes]   # omite las confirmaciones
    python3 scripts/git_cleanup.py --remote upstream
"""

from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
from dataclasses import dataclass, field
from typing import Final

GITIGNORE_WORKTREE_MARK: Final = "# Git worktrees"
WORKTREE_IGNORE_LINE: Final = ".worktrees/"
DEFAULT_BRANCH_CANDIDATES: Final = ("main", "master", "trunk")
GIT_TIMEOUT: Final = 120  # segundos; evita cuelgues silenciosos en fetch


class GitError(RuntimeError):
    """Error legible de git (sin traceback para el usuario final)."""


def info(message: str) -> None:
    print(f"  {message}")


def warn(message: str) -> None:
    print(f"  {message}", file=sys.stderr)


def _run(args: list[str], *, check: bool) -> subprocess.CompletedProcess[str]:
    """Ejecuta git de forma uniforme (utf-8, timeout, sin ventana en Windows)."""
    try:
        result = subprocess.run(
            ["git", *args],
            check=False,  # el chequeo lo hacemos nosotros para poder tipar el error
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=GIT_TIMEOUT,
        )
    except FileNotFoundError as exc:  # git no instalado / no en el PATH
        raise GitError("no se encontró el ejecutable 'git' en el PATH.") from exc
    except subprocess.TimeoutExpired as exc:
        raise GitError(f"git {' '.join(args)} tardó más de {GIT_TIMEOUT}s y se canceló.") from exc
    if check and result.returncode != 0:
        detail = (result.stderr or result.stdout).strip() or f"código {result.returncode}"
        raise GitError(f"git {' '.join(args)}: {detail}")
    return result


def git(args: list[str]) -> str:
    """Ejecuta git y devuelve stdout sin el salto de línea final."""
    return _run(args, check=True).stdout.rstrip("\n")


def git_try(args: list[str]) -> str | None:
    """Ejecuta git y devuelve stdout, o None si git falla (no imprime nada)."""
    result = _run(args, check=False)
    if result.returncode != 0:
        return None
    return result.stdout.strip()


def git_ok(args: list[str]) -> bool:
    """Ejecuta git y devuelve True si sale 0 (no imprime nada)."""
    try:
        return _run(args, check=False).returncode == 0
    except GitError:
        return False


def ensure_repo() -> str:
    """Valida que estamos en un repo git y devuelve su raíz (path absoluto)."""
    if shutil.which("git") is None:
        raise GitError("no se encontró el ejecutable 'git' en el PATH.")
    if not git_ok(["rev-parse", "--is-inside-work-tree"]):
        raise GitError("el directorio actual no está dentro de un repositorio git.")
    return git(["rev-parse", "--show-toplevel"])


def confirm(prompt: str, yes_all: bool) -> bool:
    if yes_all:
        return True
    try:
        answer = input(f"{prompt} [y/N] ").strip().lower()
    except (EOFError, KeyboardInterrupt):
        # Entrada no interactiva o Ctrl-C: no confirmado.
        return False
    return answer in ("y", "yes")


def get_current_branch() -> str:
    return git(["rev-parse", "--abbrev-ref", "HEAD"])


def get_default_branch(remote: str) -> str | None:
    """Rama por defecto: origin/HEAD si está definido; si no, main/master."""
    head = git_try(["symbolic-ref", "--quiet", f"refs/remotes/{remote}/HEAD"])
    if head:
        return head.rsplit("/", 1)[-1]
    for candidate in DEFAULT_BRANCH_CANDIDATES:
        if git_ok(["rev-parse", "--verify", "--quiet", f"refs/heads/{candidate}"]):
            return candidate
    return None


def get_branches_with_gone_tracking(remote: str) -> list[str]:
    """Ramas locales cuyo upstream remoto ya no existe (jacket [gone]).

    Una sola llamada a git: `%(*:...)` no aplica, así que listamos los upstreams
    configurados y las refs remotas existentes y comparamos en Python.
    """
    out = git([
        "for-each-ref",
        "--format=%(refname:short)|%(upstream:short)|%(upstream:track)",
        "refs/heads/",
    ])
    existing: set[str] = {
        line.strip().removeprefix("refs/remotes/")
        for line in git(["for-each-ref", "--format=%(refname:short)", "refs/remotes/"]).splitlines()
        if line.strip()
    }
    branches: list[str] = []
    for line in out.splitlines():
        if not line:
            continue
        name, _, rest = line.partition("|")
        upstream, _, track = rest.partition("|")
        if not upstream:
            continue  # sin tracking: no relacionada con un PR
        short = upstream.removeprefix("remotes/")
        if not short.startswith(f"{remote}/"):
            continue  # upstream de otro remoto: no lo tocamos
        if track.startswith("[gone]") or short not in existing:
            branches.append(name)
    return branches


def get_worktrees(branch: str) -> list[str]:
    """Paths de worktrees cuya rama es `branch`."""
    out = git(["worktree", "list", "--porcelain"])
    paths: list[str] = []
    wtree_path: str | None = None
    wtree_branch: str | None = None
    for line in out.splitlines():
        if line.startswith("worktree "):
            if wtree_path and wtree_branch == f"refs/heads/{branch}":
                paths.append(wtree_path)
            wtree_path = line[len("worktree "):]
            wtree_branch = None
        elif line.startswith("branch "):
            wtree_branch = line[len("branch "):]
    if wtree_path and wtree_branch == f"refs/heads/{branch}":
        paths.append(wtree_path)
    return paths


def is_behind(branch: str, remote: str) -> bool:
    """True si `branch` local está estrictamente detrás de `remote/branch`."""
    left, right = (int(x) for x in git(
        ["rev-list", "--left-right", "--count", f"{branch}...{remote}/{branch}"]
    ).split())
    return left == 0 and right > 0


def gitignore_has_worktree_mark(repo_root: str) -> bool:
    try:
        with open(f"{repo_root}/.gitignore", encoding="utf-8") as f:
            return GITIGNORE_WORKTREE_MARK in f.read()
    except FileNotFoundError:
        return False


def gitignore_diff(repo_root: str) -> str:
    """Diff de .gitignore contra HEAD (incluye cambios staged y unstaged)."""
    return git(["diff", "HEAD", "--", f"{repo_root}/.gitignore"])


def gitignore_only_worktree_mark(repo_root: str) -> bool:
    """True si la única diferencia en .gitignore es añadir el bloque de worktrees."""
    diff = gitignore_diff(repo_root)
    if not diff:
        return False
    for line in diff.splitlines():
        if not line.startswith(("+", "-")) or line.startswith(("+++", "---")):
            continue  # cabeceras del diff, hunk headers y líneas de contexto
        if line.startswith("-"):
            return False  # se borró algo: no es solo el bloque de worktrees
        content = line[1:].strip()
        if content and not content.startswith((WORKTREE_IGNORE_LINE, GITIGNORE_WORKTREE_MARK)):
            return False
    return True


def remove_worktree(path: str, dry: bool) -> bool:
    if dry:
        info(f"[dry] worktree remove {path}")
        return True
    try:
        _run(["worktree", "remove", path], check=True)
        info(f"[ok] worktree removido: {path}")
        return True
    except GitError as exc:
        warn(f"[skip] worktree con cambios sin commitear, no lo toco: {exc}")
        return False


def delete_branch(name: str, dry: bool) -> bool:
    if dry:
        info(f"[dry] branch -d {name}")
        return True
    try:
        _run(["branch", "-d", name], check=True)
        info(f"[ok] rama eliminada: {name}")
        return True
    except GitError as exc:
        warn(f"[skip] rama sin mergear completa, la dejo: {exc}")
        return False


@dataclass
class Report:
    """Contadores para el resumen final."""

    remote: str = ""
    fetched: bool = False
    updated_main: bool = False
    removed_worktrees: list[str] = field(default_factory=list)
    removed_branches: list[str] = field(default_factory=list)
    skipped: list[str] = field(default_factory=list)
    gitignore_restored: bool = False
    would_remove_worktrees: list[str] = field(default_factory=list)
    would_remove_branches: list[str] = field(default_factory=list)


def fetch(remote: str, dry: bool, report: Report) -> None:
    print(f"1. git fetch --prune {remote}")
    if dry:
        info(f"[dry] fetch --prune {remote}")
        return
    git(["fetch", "--prune", remote])
    report.fetched = True


def update_default_branch(remote: str, current: str, dry: bool, report: Report) -> None:
    default = get_default_branch(remote)
    if default is None or not git_ok(["rev-parse", "--verify", "--quiet", f"refs/heads/{default}"]):
        info(f"2. no hay rama por defecto local; nada que actualizar.")
        return
    if not git_ok(["rev-parse", "--verify", "--quiet", f"refs/remotes/{remote}/{default}"]):
        info(f"2. '{default}' no tiene {remote}/{default}; nada que actualizar.")
        return
    if not is_behind(default, remote):
        info(f"2. {default} está al día.")
        return

    print(f"2. {default} está detrás de {remote}/{default} (PR mergeado) → fast-forward")
    if current != default:
        info(f"[skip] estás en '{current}' (no en {default}); no cambio de rama.")
        info(f"       para actualizarlo: git switch {default} && "
             f"git merge --ff-only {remote}/{default}")
        return
    if dry:
        info(f"[dry] {default} -> {remote}/{default}")
        return
    try:
        git(["merge", "--ff-only", f"{remote}/{default}"])
    except GitError as exc:
        warn(f"[skip] fast-forward no aplicado: {exc}")
        return
    info(f"[ok] {default} actualizada")
    report.updated_main = True


def cleanup_branches(
    remote: str, current: str, default: str | None, dry: bool, yes_all: bool, report: Report
) -> None:
    gone = get_branches_with_gone_tracking(remote)
    print(f"3. Ramas locales cuyo bracket [{remote}/...] ya no existe: {len(gone)}")
    for branch in sorted(gone):
        if branch == current:
            info(f"[skip] es la rama actual, no me la borro: {branch}")
            report.skipped.append(branch)
            continue
        if branch == default:
            info("[skip] la rama por defecto no se borra.")
            report.skipped.append(branch)
            continue
        if dry:
            for wt in get_worktrees(branch):
                info(f"[dry] worktree remove {wt}")
                report.would_remove_worktrees.append(wt)
            info(f"[dry] branch -d {branch}")
            report.would_remove_branches.append(branch)
            continue
        if not confirm(f"  ¿Eliminar rama '{branch}'?", yes_all):
            info(f"[skip] cancelado por el usuario: {branch}")
            report.skipped.append(branch)
            continue
        ok = True
        for wt in get_worktrees(branch):
            if remove_worktree(wt, dry):
                report.removed_worktrees.append(wt)
            else:
                ok = False
        if ok and delete_branch(branch, dry):
            report.removed_branches.append(branch)
        else:
            report.skipped.append(branch)

    if report.removed_worktrees and not dry:
        git_ok(["worktree", "prune"])


def restore_gitignore(repo_root: str, dry: bool, yes_all: bool, report: Report) -> None:
    if not gitignore_has_worktree_mark(repo_root) or not gitignore_diff(repo_root):
        print("4. .gitignore no tiene el bloque de worktrees: nada que hacer.")
        return
    if not gitignore_only_worktree_mark(repo_root):
        info("4. .gitignore tiene cambios manuales además del bloque: no lo toco.")
        return
    print("4. .gitignore modificado solo por el bloque de worktrees → lo revierto.")
    if dry:
        info("[dry] git checkout -- .gitignore")
        return
    if not confirm("  ¿Revertir .gitignore?", yes_all):
        info("[skip] cancelado por el usuario: .gitignore")
        return
    git(["checkout", "HEAD", "--", f"{repo_root}/.gitignore"])
    info("[ok] .gitignore restaurado")
    report.gitignore_restored = True


def print_summary(report: Report, dry: bool) -> None:
    prefix = "[dry] " if dry else ""
    print("\n== Resumen ==")
    worktrees = report.would_remove_worktrees if dry else report.removed_worktrees
    branches = report.would_remove_branches if dry else report.removed_branches
    print(f"  {prefix}borrados worktrees: {len(worktrees)}")
    print(f"  {prefix}borradas ramas:    {len(branches)}")
    print(f"  {prefix}omitidas:          {len(report.skipped)}")
    if report.updated_main:
        print("  main actualizada con fast-forward")
    if report.gitignore_restored:
        print("  .gitignore restaurado")
    if report.fetched:
        print(f"  fetch --prune {report.remote}: ok")


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Limpieza de ramas locales tras mergear un PR.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument("--dry-run", action="store_true", help="muestra qué haría sin ejecutarlo")
    parser.add_argument(
        "--yes", action="store_true", help="no pide confirmación antes de cada borrado"
    )
    parser.add_argument("--remote", default="origin", help="remoto a usar (por defecto: origin)")
    args = parser.parse_args()

    dry, yes_all, remote = args.dry_run, args.yes, args.remote
    label = " (DRY-RUN: no ejecuta nada)" if dry else ""
    print(f"== Limpieza post-PR{label} ==")

    repo_root = ensure_repo()
    report = Report(remote=remote)
    fetch(remote, dry, report)

    current = get_current_branch()
    default = get_default_branch(remote)
    update_default_branch(remote, current, dry, report)
    cleanup_branches(remote, current, default, dry, yes_all, report)
    restore_gitignore(repo_root, dry, yes_all, report)

    print_summary(report, dry)
    print("\n== Listo ==")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except GitError as error:
        print(f"Error: {error}", file=sys.stderr)
        sys.exit(1)
    except KeyboardInterrupt:
        print("\nInterrumpido por el usuario.", file=sys.stderr)
        sys.exit(130)
