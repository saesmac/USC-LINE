# UNISAGRADO — Atendente v1.0 (Windows)

## O que é
Aplicativo Windows `.exe` feito em C# WinForms/WebView2, **com a tela React do atendente embutida**. Não abre nem depende do site do aluno, da Vercel, do Codespaces ou da porta 5173 para exibir a interface. O mesmo pacote pode ser usado nos seis computadores. Cada computador seleciona sua mesa, que permanece salva localmente no WebView2 daquele PC.

## Dependência externa
O atendimento requer acesso pela rede a uma **API ASP.NET Core central**, que conversa com o Supabase. O arquivo `AtendenteWindows/config.json` começa com `http://127.0.0.1:5001`, útil para testes SOMENTE quando a API roda no próprio PC. Quando a API for hospedada, altere `apiUrl` para o endereço HTTPS definitivo (sem `/api`). Não colocar credenciais Supabase no aplicativo.

Exemplo:
```json
{ "apiUrl": "https://SUA-API.example.com" }
```

**A alteração de config.json dispensa recompilação**. Mantenha `config.json` ao lado do `.exe` em cada PC.

## Instalar no Codespaces (sem substituir o React)
Extraia o zip em `/workspaces/USC-LINE` e execute:
```bash
cd /workspaces/USC-LINE
bash unisagrado-exe/INSTALAR_NO_REPOSITORIO.sh /workspaces/USC-LINE
```

O instalador **acrescenta** arquivos, não substitui o React existente. Compilação opcional do React:
```bash
cd /workspaces/USC-LINE/usc_line
npm ci
npm run build -- --config vite.desktop.config.js
```
Deve surgir `AtendenteWindows/wwwroot` com `atendente-desktop.html` e `assets/`.

## Gerar o `.exe` no GitHub
```bash
cd /workspaces/USC-LINE
git add AtendenteWindows/AtendenteWindows.csproj AtendenteWindows/Program.cs AtendenteWindows/config.json \
  usc_line/atendente-desktop.html usc_line/src/atendente-desktop.jsx usc_line/vite.desktop.config.js \
  .github/workflows/compilar-atendente-local.yml
git commit -m "Adiciona app Windows com React do atendente embutido"
git push origin front
```
O diretório `AtendenteWindows/wwwroot/` é gerado por compilação e não precisa ir ao Git. Use GitHub > Actions > **Compilar UNISAGRADO Atendente para Windows** > **Run workflow** > branch `front`.

Baixe o *artifact* `UNISAGRADO-Atendente-v1.0-win-x64`, extraia seu ZIP e depois extraia o ZIP interno. Distribua a pasta publicada **inteira** (incluindo `wwwroot` e `config.json`); execute `UNISAGRADO.Atendente.exe`.

## Windows
Requer Windows x64 e WebView2 Runtime (geralmente já existe no Windows 11). O .NET é autocontido. O `.exe` não foi compilado/testado nesta sessão: a build real ocorrerá no GitHub Actions em Windows.

## Segurança
Conforme solicitado, **não há autenticação** nesta versão. Isso não impede chamadas administrativas por pessoas que consigam acessar a API. Não exponha a API administrativa à internet sem proteção; use apenas em ambiente de teste ou uma rede controlada e planeje autenticação antes de produção.

## Nota sobre Excel
O empacotamento usa o mesmo `AtendenteApp.jsx` e as dependências do repositório. Se você já instalou a correção da planilha Excel, ela virá junto. O wrapper só encaminha requisições `/api` para o endereço configurado.
