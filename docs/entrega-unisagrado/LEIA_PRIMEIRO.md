# UNISAGRADO — Entrega 1: frontend do aluno

Este pacote foi preparado para **substituir somente o frontend do aluno** da pasta `usc_line` do repositório USC-LINE. Não altera o backend, o banco de dados, o GitHub ou outras branches automaticamente.

## 1. Antes de instalar

- Verifique se está na branch de trabalho correta (`git branch --show-current`). Preferencialmente crie sua própria branch a partir da versão integrada da `main`.
- Verifique `git status` e faça commit das alterações pendentes antes de instalar.
- Extraia este ZIP **na raiz do repositório** `/workspaces/USC-LINE`, mantendo a pasta `arquivo-aluno` e o script `INSTALAR_NO_CODESPACE.sh` juntos.

## 2. Instalação automatizada, preservando o package.json

No terminal, na pasta onde extraiu o ZIP:

```bash
bash INSTALAR_NO_CODESPACE.sh /workspaces/USC-LINE/usc_line
cd /workspaces/USC-LINE/usc_line
npm ci
npm run build
npm run dev -- --host 0.0.0.0
```

O instalador cria a pasta `backup-react-DATA-HORA` ao lado dele com cópias dos arquivos originais. Não altera `package.json`, `package-lock.json`, nem modifica configurações do backend.

## 3. API C# no mesmo Codespace

Em outro terminal:

```bash
cd /workspaces/USC-LINE/Backend/Secretaria.Api
dotnet run --urls http://0.0.0.0:5001
```

O frontend acessa `/api` pelo proxy do Vite, que encaminha para `http://127.0.0.1:5001`. A API precisa ter a connection string do Supabase configurada no seu `appsettings.Development.json` local, não versionado.

Se a API estiver em **outro Codespace**, edite `.env.local` dentro de `usc_line`:

```text
VITE_API_PROXY_TARGET=https://URL-DA-PORTA-5001.app.github.dev
```

A URL precisa estar acessível e o Vite precisa ser reiniciado. Se o outro Codespace estiver privado, o proxy pode não conseguir acessá-lo sem autenticação.

## 4. Endpoints utilizados

| Operação | Endpoint | Corpo |
|---|---|---|
| Retirar senha | POST `/api/Senhas` | `{ "nome": "Ana", "descricao": "Matrícula", "preferencial": false }` |
| Acompanhar | GET `/api/Acompanhamento/N001` | — |
| Cancelar | POST `/api/Senhas/cancelar` | `{ "codigo": "N001", "motivo": "Cancelado pelo aluno" }` |
| Fila do dia | GET `/api/Senhas` | — |

**Conferir na API real:** o DTO de cancelamento e os campos JSON de acompanhamento. O código faz uma normalização de respostas comuns, mas não substitui um teste integrado.

## 5. Cuidados importantes

- Nenhum número de senha é criado no navegador; a API/Postgres criam o N001/P001.
- A tela acompanha a senha consultando a API a cada 5 segundos enquanto a página está visível.
- Vibração depende das permissões do navegador e costuma exigir uma interação prévia do usuário. Não promete avisos com a tela fechada.
- A senha atual é lembrada no `localStorage` do mesmo navegador. Limpe o armazenamento ao testar alunos diferentes no mesmo aparelho.
- A listagem pública exibe só código/status/mesa, mas `GET /api/Senhas` pode retornar nomes e descrições: para produção, criar uma resposta pública sanitizada e revisar o acesso ao acompanhamento.
- **Nunca coloque connection strings, tokens de serviço ou senhas do banco em `VITE_*` nem em arquivos do frontend.**
- Codespaces é ambiente de desenvolvimento, não hospedagem permanente.
- Em produção, configurar HTTPS, URL pública da API e CORS restrito à origem do site do aluno.

## 6. Teste ponta a ponta

1. API em execução: acessar `GET /api/DatabaseHealth` e conferir status.
2. Abrir React (porta 5173) e solicitar senha.
3. Verificar se o código retornado corresponde a um registro real no Supabase.
4. No backend, chamar a senha para uma mesa e observar a tela mudar para verde.
5. Finalizar atendimento e confirmar que a tela atualiza o status.
6. Criar outra senha, cancelar e confirmar a atualização no banco.
7. Testar um segundo celular/navegador para validar que a fila é compartilhada.

## 7. Próximas entregas

1. **Backend — validação de contrato**: conferir DTOs e respostas; endurecer endpoints públicos e CORS/autenticação conforme perfil.
2. **Aluno — teste real**: emissão, acompanhamento, chamada, cancelamento, atualização no celular.
3. **Atendente — React**: escolher mesa, fila, chamada automática/manual, iniciar/finalizar, registrar resultado/observações.
4. **Atendente — Windows**: hospedar a interface em C# WebView2 e criar empacotamento/instalador.
5. **TV — React**: senha atual, mesa, últimas chamadas, publicidade e modo tela cheia.
6. **Sincronização**: refinar polling ou adotar realtime, controlar mudanças de status e concorrência.
7. **Administração**: gerenciar propagandas, histórico, relatórios e exportação CSV.
8. **Segurança e operação**: perfis, autorização, LGPD, logs, recuperação, backups, hospedagem da API e frontend.
9. **Homologação**: testar com fluxo real da Secretaria e seis mesas.
10. **Entrega**: QR Code, distribuição do app, instruções, treinamento e documentação final.
