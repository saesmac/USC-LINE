#!/usr/bin/env bash
set -euo pipefail
ROOT="${1:-/workspaces/USC-LINE/usc_line}"
if [[ ! -f "$ROOT/package.json" || ! -f "$ROOT/src/AtendenteApp.jsx" ]]; then
  echo 'ERRO: informe o caminho da pasta React (usc_line).'
  exit 1
fi
HERE="$(cd "$(dirname "$0")" && pwd)"
BACKUP="$ROOT/backup-relatorios-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$BACKUP"
for f in AtendenteApp.jsx AtendenteApp.css atendenteApi.js exportarExcel.js; do
  if [[ -f "$ROOT/src/$f" ]]; then cp "$ROOT/src/$f" "$BACKUP/$f"; fi
  cp "$HERE/src/$f" "$ROOT/src/$f"
done
printf 'Arquivos instalados. Backup: %s\n' "$BACKUP"
printf 'PRÓXIMO: cd "%s" && npm install exceljs && npm run build\n' "$ROOT"
