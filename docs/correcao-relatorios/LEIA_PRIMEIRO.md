# Correção de relatórios — Secretaria UNISAGRADO

## Problemas corrigidos
- O botão Gerar relatório agora aplica explicitamente as datas escolhidas, mostra aviso de carregamento e quantos registros foram encontrados.
- O Excel é um `.xlsx` verdadeiro, com título, datas legíveis, filtros, colunas dimensionadas e linhas alternadas.
- O arquivo usa a resposta do endpoint **já existente** `/api/Relatorios/csv`, respeitando o período aplicado; não muda o Supabase nem o backend.
- Erros das consultas aparecem na tela, e dados antigos são limpos se uma consulta falhar.

## Instalação no Codespaces
Com os arquivos da pasta `correcao-relatorios` extraídos na raiz `/workspaces/USC-LINE`:

```bash
cd /workspaces/USC-LINE
bash correcao-relatorios/INSTALAR.sh /workspaces/USC-LINE/usc_line
cd /workspaces/USC-LINE/usc_line
npm install exceljs
npm run build
npm run dev -- --host 0.0.0.0
```

**Importante**: `npm install exceljs` precisa ser executado para adicionar a biblioteca ao `package.json` e `package-lock.json`. Não é preciso instalar outra API ou tabela. O instalador faz backup dos arquivos substituídos, mas não modifica `package.json` automaticamente.

## Teste
1. Abra `/atendente` e a aba **Relatórios**. Deixe a API C# na porta 5001 rodando.
2. Selecione uma data com registros e clique **Gerar relatório**; confira a mensagem de atualização, indicadores e tabela.
3. Altere para um período sem registros e clique novamente. A tabela deve esvaziar.
4. Escolha o período de interesse, gere o relatório e clique **Baixar planilha Excel (.xlsx)**.
5. Abra no Excel e verifique os filtros, as datas e o tamanho das colunas.

## Cuidados
O painel `/atendente` ainda precisa de autenticação protegida no backend antes de ser disponibilizado publicamente. O arquivo Excel contém dados pessoais e deve ser guardado em local restrito.
O download depende de `/api/Relatorios/csv` e a página de relatórios depende dos endpoints `/api/Relatorios` e `/api/Historico`.
