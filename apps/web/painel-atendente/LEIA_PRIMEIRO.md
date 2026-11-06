# UNISAGRADO — Painel do Atendente (etapa 2)

Este pacote **não substitui a tela do aluno**. Ele instala `AtendenteApp.jsx`, `AtendenteApp.css`, `atendenteApi.js` e altera apenas `main.jsx` para habilitar o endereço `/atendente`. A tela do aluno continua em `/`.

## Instalação no Codespaces
1. Envie o ZIP para `/workspaces/USC-LINE/` e extraia: `unzip USC-LINE-Painel-Atendente.zip`
2. Execute: `bash painel-atendente/INSTALAR.sh /workspaces/USC-LINE/usc_line`
3. Execute: `cd /workspaces/USC-LINE/usc_line && npm ci && npm run build && npm run dev -- --host 0.0.0.0`
4. Inicie a API em outro terminal: `cd /workspaces/USC-LINE/Backend/Secretaria.Api && dotnet run --urls http://0.0.0.0:5001`
5. Na porta 5173, abra `https://SEU-ENDERECO-5173.app.github.dev/atendente`.

## API usada
- GET `/api/Senhas`, GET `/api/Mesas`
- POST `/api/Senhas/chamar` com `{ "mesaNumero": 1 }`
- POST `/api/Senhas/chamar-manual` com `{ "codigo": "N001", "mesaNumero": 1 }`
- POST `/api/Senhas/iniciar` com `{ "mesaNumero": 1 }`
- POST `/api/Senhas/finalizar` com `{ "mesaNumero": 1, "resultado": "Atendimento realizado", "observacao": "..." }`
- GET `/api/Relatorios`, GET `/api/Historico`, GET `/api/Relatorios/csv`

Os nomes dos campos `mesaNumero`, `resultado` e `observacao` seguem as convenções já usadas durante a construção da API, mas **é obrigatório validar com os DTOs presentes no repositório**. Se a resposta de `/api/Mesas` usar outra estrutura para a senha atual, ajuste a função `currentTicket()` em `AtendenteApp.jsx`.

A interface consulta a fila a cada 4 segundos. Não é necessário Supabase no React nem bibliotecas adicionais.

**ATENÇÃO — segurança:** esta rota ainda não tem autenticação. Não publique `/atendente` publicamente ou use em produção antes de implementar controle de acesso e autorização no backend. Em Codespaces, mantenha a porta 5173 privada quando testar operações administrativas.

**Aplicativo Windows:** ainda não incluído neste pacote. Após testar o painel no navegador, a etapa seguinte será embuti-lo em WebView2 e preparar o aplicativo/instalador Windows.
