#!/usr/bin/env bash
set -euo pipefail
ROOT="${1:-/workspaces/USC-LINE}"
SOURCE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [[ ! -f "$ROOT/usc_line/package.json" || ! -f "$ROOT/usc_line/src/AtendenteApp.jsx" || ! -f "$ROOT/usc_line/src/atendenteApi.js" ]]; then
  echo "ERRO: nao encontrei o React do atendente em $ROOT/usc_line. Nenhum arquivo alterado."
  exit 1
fi
if [[ -d "$ROOT/AtendenteWindows" ]]; then
  echo "ERRO: $ROOT/AtendenteWindows ja existe. Confira antes de substituir."
  exit 1
fi
for item in "$ROOT/usc_line/atendente-desktop.html" "$ROOT/usc_line/src/atendente-desktop.jsx" "$ROOT/usc_line/vite.desktop.config.js" "$ROOT/.github/workflows/compilar-atendente-local.yml"; do
  if [[ -e "$item" ]]; then
    echo "ERRO: arquivo ja existente: $item. Nenhum arquivo alterado."
    exit 1
  fi
done
mkdir -p "$ROOT/.github/workflows"
cp -r "$SOURCE/AtendenteWindows" "$ROOT/AtendenteWindows"
cp "$SOURCE/FrontendDesktop/atendente-desktop.html" "$ROOT/usc_line/"
cp "$SOURCE/FrontendDesktop/atendente-desktop.jsx" "$ROOT/usc_line/src/"
cp "$SOURCE/FrontendDesktop/vite.desktop.config.js" "$ROOT/usc_line/"
cp "$SOURCE/.github/workflows/compilar-atendente-local.yml" "$ROOT/.github/workflows/"
echo "INSTALADO. O projeto original do aluno e do atendente foi preservado."
echo "Teste o React: cd '$ROOT/usc_line' && npm ci && npm run build -- --config vite.desktop.config.js"
echo "No GitHub: Actions > Compilar UNISAGRADO Atendente para Windows > Run workflow (branch front)"
