#!/usr/bin/env python3
"""
Automatiza la creación de nuevas ramas de trabajo (feat, fix, chore, etc.)
a partir de la rama actual, asegurando que esté actualizada y limpia.

Flujo:
1. Verifica el estado del árbol de trabajo.
   - Si hay cambios, pregunta si guardarlos con `git stash`.
2. Actualiza la rama actual (fast-forward only).
3. Pide tipo y nombre de la nueva rama (o los toma de argumentos).
4. Crea y cambia a la nueva rama con `git switch -c`.
5. Si se hizo stash, restaura los cambios con `git stash pop`.
6. (Opcional) Sube la rama al remoto y configura el tracking.

Seguridad:
- Usa `--ff-only` para evitar merges accidentales al actualizar.
- Si `stash pop` genera conflictos, avisa y no los resuelve solo.
- Sanitiza el nombre de la rama a kebab-case (minúsculas y guiones).
- Nunca deja el stash huérfano: si algo falla después del stash, lo restaura.
"""

from __future__ import annotations

import argparse
import re
import sys
from enum import Enum

try:  # ejecutado como script (python3 scripts/git_new_branch.py)
    from _git import GitError, confirm, ensure_repo, git, git_ok, git_try, info, warn
except ImportError:  # ejecutado como modulo (python3 -m scripts.git_new_branch)
    from scripts._git import GitError, confirm, ensure_repo, git, git_ok, git_try, info, warn


class BranchType(str, Enum):
    FEAT = "feat"
    FIX = "fix"
    CHORE = "chore"
    REFACTOR = "refactor"
    HOTFIX = "hotfix"
    DOCS = "docs"


VALID_TYPES = tuple(branch_type.value for branch_type in BranchType)


def has_uncommitted_changes() -> bool:
    """Detecta cambios staged, unstaged o untracked."""
    return bool(git(["status", "--porcelain"]).strip())


def get_change_summary() -> str:
    """Resumen legible de los cambios pendientes."""
    lines = [line for line in git(["status", "--short"]).splitlines() if line.strip()]
    if not lines:
        return ""

    staged = sum(1 for line in lines if line[0] not in " ?")
    unstaged = sum(1 for line in lines if line[0] != "?" and line[1] != " ")
    untracked = sum(1 for line in lines if line.startswith("??"))

    parts = []
    if staged:
        parts.append(f"{staged} staged")
    if unstaged:
        parts.append(f"{unstaged} sin stage")
    if untracked:
        parts.append(f"{untracked} untracked")
    return ", ".join(parts)


def stash_push(message: str, dry: bool) -> bool:
    """Guarda los cambios locales (incluidos los archivos nuevos)."""
    if dry:
        info(f"[dry] stash push -m {message!r}")
        return True
    if git_try(["stash", "push", "--include-untracked", "-m", message]) is None:
        warn("[error] No se pudo guardar el stash. Revisa el estado manualmente.")
        return False
    info("[ok] Cambios guardados en stash.")
    return True


def stash_pop(dry: bool) -> bool:
    """Restaura el stash. Devuelve False si queda conflicto."""
    if dry:
        info("[dry] stash pop")
        return True

    if git_try(["stash", "pop"]) is not None:
        info("[ok] Cambios restaurados del stash.")
        return True

    # El mensaje de error de git varia segun la version: se mira el estado real.
    conflicted = git_try(["diff", "--name-only", "--diff-filter=U"]) or ""
    if conflicted.strip():
        warn("[warn] Conflicto al restaurar el stash. Resuelvelo manualmente y luego:")
        warn("       git stash drop")
    else:
        warn("[error] No se pudo restaurar el stash; revisa 'git stash list'.")
    return False



def get_current_branch() -> str:
    branch = git(["rev-parse", "--abbrev-ref", "HEAD"])
    if branch == "HEAD":
        raise GitError("estás en detached HEAD: no se puede crear una rama desde aquí.")
    return branch


def update_current_branch(branch: str, dry: bool) -> bool:
    """Actualiza la rama actual con fast-forward, si tiene upstream."""
    info(f"Actualizando '{branch}' desde el remoto (fast-forward)...")
    if dry:
        info("[dry] pull --ff-only")
        return True

    if not git_ok(["rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}"]):
        info(f"[skip] '{branch}' no tiene upstream configurado; no hago pull.")
        return True

    if git_try(["pull", "--ff-only"]) is not None:
        info(f"[ok] '{branch}' está al día.")
        return True

    warn("[error] La rama local ha divergido del remoto. Resuélvelo manualmente.")
    return False


def sanitize_branch_name(name: str) -> str:
    """Convierte cualquier string a un nombre de rama válido (kebab-case)."""
    clean = re.sub(r"[\s/_]+", "-", name.lower().strip())
    clean = re.sub(r"[^a-z0-9\-]", "", clean)
    return clean.strip("-")


def prompt_user(message: str, options: tuple[str, ...] | None = None) -> str:
    """Prompt interactivo; si la entrada no es interactiva, pide los flags."""
    while True:
        try:
            if options:
                joined = ", ".join(options)
                answer = input(f"{message} [{joined}]: ").strip().lower()
                if answer in options:
                    return answer
                warn(f"Opción inválida. Elige una de: {joined}")
            else:
                answer = input(f"{message}: ").strip()
                if answer:
                    return answer
                warn("El nombre no puede estar vacío.")
        except EOFError as exc:
            raise GitError(
                "entrada no interactiva: usa --type y --name (por ejemplo -t fix -n login-bug)."
            ) from exc


def branch_exists(name: str, remote: str) -> bool:
    """Verifica si la rama ya existe localmente o en el remoto."""
    return git_ok(["show-ref", "--verify", "--quiet", f"refs/heads/{name}"]) or git_ok(
        ["show-ref", "--verify", "--quiet", f"refs/remotes/{remote}/{name}"]
    )


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Crea y prepara una nueva rama de trabajo a partir de la actual.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="Ejemplos:\n"
        "  python3 scripts/git_new_branch.py\n"
        "  python3 scripts/git_new_branch.py -t fix -n 'login-bug'\n"
        "  python3 scripts/git_new_branch.py --push --yes",
    )
    parser.add_argument("--type", "-t", choices=VALID_TYPES, help="Tipo de rama (feat, fix, chore, ...)")
    parser.add_argument("--name", "-n", help="Nombre descriptivo (se sanitiza a kebab-case)")
    parser.add_argument("--push", "-p", action="store_true", help="Hace push inmediato y configura upstream")
    parser.add_argument("--yes", "-y", action="store_true", help="Omite las confirmaciones interactivas")
    parser.add_argument("--remote", default="origin", help="Remoto a usar (por defecto: origin)")
    parser.add_argument("--dry-run", action="store_true", help="Muestra qué haría sin ejecutar cambios")
    return parser.parse_args()



def main() -> int:
    args = parse_arguments()
    dry, yes_all, remote = args.dry_run, args.yes, args.remote

    print(f"== Creación de nueva rama{' (DRY-RUN)' if dry else ''} ==\n")
    ensure_repo()

    print("1. Verificando estado del repositorio...")
    current = get_current_branch()
    info(f"Rama base actual: '{current}'")

    did_stash = False
    if has_uncommitted_changes():
        warn(f"[warn] Tienes cambios sin commitear ({get_change_summary()}).")
        if not confirm("  ¿Guardar cambios con 'git stash' y restaurarlos en la nueva rama?", yes_all):
            warn("[abort] Operación cancelada. Haz commit o stash manualmente.")
            return 1
        if not stash_push(f"auto-stash: cambios pendientes antes de crear rama desde {current}", dry):
            return 1
        did_stash = True
    else:
        info("[ok] Árbol de trabajo limpio.")

    full_branch_name = ""
    try:
        print("\n2. Sincronizando rama base...")
        if not update_current_branch(current, dry):
            return 1

        print("\n3. Configurando nueva rama...")
        branch_type = args.type or prompt_user("Selecciona el tipo de rama", VALID_TYPES)
        raw_name = args.name or prompt_user("Describe brevemente el trabajo (ej. 'Add login page')")

        clean_name = sanitize_branch_name(raw_name)
        if not clean_name:
            raise GitError(f"'{raw_name}' no genera un nombre de rama válido.")
        full_branch_name = f"{branch_type}/{clean_name}"
        info(f"Nombre final propuesto: '{full_branch_name}'")

        if branch_exists(full_branch_name, remote):
            raise GitError(f"la rama '{full_branch_name}' ya existe (local o en {remote}).")

        print("\n4. Creando y cambiando a la nueva rama...")
        if dry:
            info(f"[dry] switch -c {full_branch_name}")
        else:
            git(["switch", "-c", full_branch_name])
            info(f"[ok] Cambiado a la nueva rama: '{full_branch_name}'")
    finally:
        # Nunca dejar el stash colgado, aunque falle un paso intermedio.
        if did_stash:
            print("\n5. Restaurando cambios guardados...")
            if not stash_pop(dry):
                warn("[warn] La rama fue creada pero el stash no se restauró.")

    if args.push:
        step = "6" if did_stash else "5"
        print(f"\n{step}. Subiendo la rama al remoto y configurando tracking...")
        if dry:
            info(f"[dry] push -u {remote} {full_branch_name}")
        else:
            git(["push", "-u", remote, full_branch_name])
            info(f"[ok] Rama publicada en {remote}/{full_branch_name}")

    print("\n== Listo: a programar. ==")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except GitError as error:
        print(f"Error: {error}", file=sys.stderr)
        sys.exit(1)
    except KeyboardInterrupt:
        print("\nOperación cancelada por el usuario.", file=sys.stderr)
        sys.exit(130)
