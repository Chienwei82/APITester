#!/usr/bin/env python3
"""Wrappers de git compartidos por los scripts de mantenimiento.

Centraliza la invocacion de `git` (UTF-8, timeout, errores tipados) para que
`git_cleanup.py` y `git_new_branch.py` se comporten igual y no dupliquen codigo.

Uso: `from _git import git, git_try, git_ok, GitError, info, warn`.
"""

from __future__ import annotations

import shutil
import subprocess
import sys
from typing import Final

GIT_TIMEOUT: Final = 120  # segundos; evita cuelgues silenciosos en fetch/push


class GitError(RuntimeError):
    """Error legible de git (sin traceback para el usuario final)."""


def info(message: str) -> None:
    print(f"  {message}")


def warn(message: str) -> None:
    print(f"  {message}", file=sys.stderr)


def _run(args: list[str], *, check: bool) -> subprocess.CompletedProcess[str]:
    """Ejecuta git de forma uniforme (utf-8, timeout, errores reemplazados)."""
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
    """Valida que estamos en un repo git y devuelve su raiz (path absoluto)."""
    if shutil.which("git") is None:
        raise GitError("no se encontró el ejecutable 'git' en el PATH.")
    if not git_ok(["rev-parse", "--is-inside-work-tree"]):
        raise GitError("el directorio actual no está dentro de un repositorio git.")
    return git(["rev-parse", "--show-toplevel"])


def confirm(prompt: str, yes_all: bool) -> bool:
    """Pregunta si/no; con `yes_all` (o sin entrada interactiva) no bloquea."""
    if yes_all:
        return True
    try:
        answer = input(f"{prompt} [y/N] ").strip().lower()
    except (EOFError, KeyboardInterrupt):
        # Entrada no interactiva o Ctrl-C: no confirmado.
        return False
    return answer in ("y", "yes")


if __name__ == "__main__":
    warn("_git.py es un modulo de apoyo: importalo desde los scripts, no lo ejecutes.")
    sys.exit(2)
