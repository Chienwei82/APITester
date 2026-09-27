# scripts/

Utilidades de mantenimiento del repo (Python 3.10+, sin dependencias externas).

| Script | Que hace |
| --- | --- |
| [`git_cleanup.py`](git_cleanup.py) | Limpieza post-merge de PR: fetch `--prune`, fast-forward de la rama por defecto, borrado de ramas locales cuyo tracking remoto ya no existe (con sus worktrees) y restauracion de `.gitignore` si solo tiene el bloque de worktrees. |

## Uso

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

## Garantias de seguridad

- Nunca borra la rama actual ni la rama por defecto.
- Nunca usa `git branch -D`: una rama sin mergear se reporta y se deja intacta.
- Nunca cambia de rama por sorpresa: si no estás en la rama por defecto, imprime el comando exacto para actualizarla.
- Un worktree con cambios sin commitear no se toca (y por tanto tampoco se borra su rama).
- `.gitignore` solo se restaura si el diff contra `HEAD` agrega únicamente el bloque de worktrees (nada más).
- Sin repo git, sin `git` en el PATH o con un error de git: mensaje claro y exit code 1, nunca un traceback.

## Seguimiento de mejoras

Estado de la revisión de `git_cleanup.py` (2026-09-26).

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
| 13 | Tests automaticos del script (repo de pruebas con remoto bare) en `tests/test_git_cleanup.py` | Pendiente |
| 14 | Modo `--json` para integrable en automatizaciones | Pendiente |
