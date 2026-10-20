# scripts/

Utilidades de mantenimiento del repo (Python 3.10+, sin dependencias externas).

| Script | Que hace |
| --- | --- |
| [`git_cleanup.py`](git_cleanup.py) | Limpieza post-merge de PR: fetch `--prune`, fast-forward de la rama por defecto, borrado de ramas locales cuyo tracking remoto ya no existe (con sus worktrees) y restauracion de `.gitignore` si solo tiene el bloque de worktrees. |
| [`git_new_branch.py`](git_new_branch.py) | Crea una rama `tipo/nombre` a partir de la actual: actualiza la base con fast-forward, guarda/restaura cambios sin commitear con `stash` y opcionalmente publica la rama (`--push`). |
| [`_git.py`](_git.py) | Modulo interno: wrappers de `git` compartidos (UTF-8, timeout, `GitError`) que usan los dos scripts. No se ejecuta. |

## git_cleanup.py

```bash
# ver que haria, sin tocar nada (empieza siempre por aqui)
python3 scripts/git_cleanup.py --dry-run

# ejecucion real, con confirmacion por rama
python3 scripts/git_cleanup.py

# sin confirmaciones (util en CI o cuando ya revisaste el dry-run)
python3 scripts/git_cleanup.py --yes

# otro remoto
python3 scripts/git_cleanup.py --remote upstream
```

### Garantias de seguridad

- Nunca borra la rama actual ni la rama por defecto.
- Usa `git branch -d` y solo recurre a `-D` cuando demuestra antes que el contenido
  de la rama ya esta integrado en la rama por defecto (`merge-tree --write-tree`
  identico al arbol de la base). Es necesario porque un merge con **squash** no deja
  los commits originales como ancestros y `-d` los rechaza.
- Una rama con cambios propios sin integrar se reporta y se deja intacta.
- Nunca cambia de rama por sorpresa: si no estas en la rama por defecto, imprime el
  comando exacto para actualizarla.
- Un worktree con cambios sin commitear no se toca (y por tanto tampoco se borra su rama).
- `.gitignore` solo se restaura si el diff contra `HEAD` agrega unicamente el bloque de worktrees.
- Sin repo git, sin `git` en el PATH o con un error de git: mensaje claro y exit code 1, nunca un traceback.

## git_new_branch.py

```bash
# interactivo: pregunta tipo y nombre
python3 scripts/git_new_branch.py

# no interactivo (CI o prisa): tipo y nombre por flags
python3 scripts/git_new_branch.py -t fix -n 'login bug'

# ademas publica la rama y configura el tracking
python3 scripts/git_new_branch.py -t feat -n 'export csv' --push

# ver que haria
python3 scripts/git_new_branch.py -t chore -n 'bump deps' --dry-run
```

- El nombre se sanitiza a kebab-case (`Login  bug!` -> `login-bug`).
- `--ff-only` al actualizar: si la rama base divergio, avisa y no fuerza nada.
- Si hay cambios sin commitear pregunta si guardarlos en `stash`; el `stash` se
  restaura siempre al final, incluso si un paso intermedio falla.
- Si `stash pop` deja conflictos, los reporta (`git diff --diff-filter=U`) y no los resuelve solo.
- `--remote` (default `origin`) para repos con otro remoto.

## Seguimiento de mejoras

Estado de la revision de los scripts (2026-10-20).

| # | Mejora | Estado |
| --- | --- | --- |
| 1 | Errores de git tipados (`GitError`) con mensaje limpio y exit code en vez de traceback | Hecho |
| 2 | `git` no encontrado / no estamos en un repo: validacion explicita con `ensure_repo()` | Hecho |
| 3 | Deteccion de "tracking gone" con 2 llamadas de git en vez de un `rev-parse` por rama | Hecho |
| 4 | Rutas absolutas desde `rev-parse --show-toplevel`: funciona desde cualquier CWD | Hecho |
| 5 | `.gitignore`: diff contra `HEAD` (cubre staged) y rechazo si hay lineas borradas o ajenas al bloque | Hecho |
| 6 | Rama por defecto detectada (`origin/HEAD`, si no `main`/`master`/`trunk`) en vez de `main` hardcodeado | Hecho |
| 7 | Flag `--remote` para repos con remoto distinto de `origin` | Hecho |
| 8 | `git worktree prune` tras remover worktrees, para limpiar refs obsoletas | Hecho |
| 9 | `Report` + resumen final (borrados / omitidos / main actualizada) | Hecho |
| 10 | `encoding`/`errors` explicitos, `timeout` en cada invocacion de git (fetch incluido) | Hecho |
| 11 | Sugerencia del comando a mano cuando no se puede hacer el fast-forward por rama actual | Hecho |
| 12 | `main()` devuelve exit code; helpers `git_try` / `git_ok` sin duplicar subprocess | Hecho |
| 13 | Borrado de ramas mergeadas con squash (`merge-tree`), sin renunciar a `-d` cuando no se puede verificar | Hecho |
| 14 | Helpers de git compartidos por ambos scripts en `_git.py` (sin duplicacion) | Hecho |
| 15 | `git_new_branch.py`: `GitError`, `--remote`, validacion de nombre y conflictos de stash detectados por estado | Hecho |
| 16 | Tests automaticos de los scripts (repo de pruebas con remoto bare) en `tests/test_git_scripts.py` | Pendiente |
| 17 | Modo `--json` para integrable en automatizaciones | Pendiente |
