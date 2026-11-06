#!/usr/bin/env bash
set -euo pipefail
# Execute este script a partir da pasta descompactada, na raiz do repositorio USC-LINE.
ROOT="$(cd "$(dirname "$0")" && pwd)"
DEST="${1:-$ROOT/usc_line}"
if [[ ! -f "$DEST/package.json" ]]; then
  echo "ERRO: pasta React nao encontrada em: $DEST"
  echo "Uso: bash INSTALAR_NO_CODESPACE.sh /workspaces/USC-LINE/usc_line"
  exit 1
fi
BACKUP="$ROOT/backup-react-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$BACKUP/src"
for file in App.jsx App.css index.css main.jsx api.js; do
  if [[ -f "$DEST/src/$file" ]]; then cp "$DEST/src/$file" "$BACKUP/src/$file"; fi
done
for file in vite.config.js index.html; do
  if [[ -f "$DEST/$file" ]]; then cp "$DEST/$file" "$BACKUP/$file"; fi
done
mkdir -p "$DEST/src"
cp "$ROOT/arquivo-aluno/src/"* "$DEST/src/"
cp "$ROOT/arquivo-aluno/vite.config.js" "$DEST/vite.config.js"
cp "$ROOT/arquivo-aluno/index.html" "$DEST/index.html"
if [[ ! -f "$DEST/.env.local" ]]; then
  cp "$ROOT/arquivo-aluno/.env.example" "$DEST/.env.local"
fi
printf '\nInstalado em %s\nBackup em %s\n' "$DEST" "$BACKUP"
printf 'Proximos comandos: cd "%s" && npm ci && npm run build && npm run dev -- --host 0.0.0.0\n' "$DEST"
