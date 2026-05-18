#!/bin/bash
SESSION="opencode"

if tmux has-session -t "$SESSION" 2>/dev/null; then
    echo "La sesión tmux '$SESSION' ya existe."
    echo "  Reconectar: tmux attach -t $SESSION"
    echo "  Ver output: tmux capture-pane -t $SESSION -p"
else
    tmux new-session -d -s "$SESSION" 'opencode --hostname 0.0.0.0 --port 4096'
    echo "✓ Sesión tmux '$SESSION' creada."
    echo "  Reconectar: tmux attach -t $SESSION"
    echo "  Salir sin cerrar: Ctrl+B, luego D"
fi
