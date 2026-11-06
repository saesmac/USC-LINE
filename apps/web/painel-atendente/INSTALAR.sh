#!/usr/bin/env bash
set -euo pipefail
TARGET="${1:-/workspaces/USC-LINE/usc_line}"
if [[ ! -f "$TARGET/package.json" || ! -f "$TARGET/src/App.jsx" ]]; then
  echo "ERRO: não encontrei o React existente em $TARGET"
  exit 1
fi
SRC="$(cd "$(dirname "$0")" && pwd)/arquivos/src"
BACKUP="$TARGET/backup-painel-atendente-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$BACKUP"
for f in main.jsx AtendenteApp.jsx AtendenteApp.css atendenteApi.js; do
  if [[ -f "$TARGET/src/$f" ]]; then cp "$TARGET/src/$f" "$BACKUP/$f"; fi
done
cp "$SRC/"* "$TARGET/src/"
echo "Instalado! Backup de arquivos substituídos: $BACKUP"
echo "Aluno: /    Atendente: /atendente"
echo "Execute: cd '$TARGET' && npm run build && npm run dev -- --host 0.0.0.0"
