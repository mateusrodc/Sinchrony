# Aluno bloqueado não é mais impedido de logar

Data: 23/09/2026 · Escopo: back-end (API) · Status: implementado localmente, ainda não commitado

## 1. Resumo

`DEMANDA_LOGIN_LIBERADO_ALUNO_BLOQUEADO_BACKEND.md`: bloqueio (`StudentStatus.blocked`, por
qualquer motivo — falha de pagamento ou manual) deixou de impedir login/refresh/Google login.
Continua impedindo reserva de aula, que é a única barreira real pedida pelo cliente.

## 2. Mudanças no código

| Arquivo | Mudança |
|---|---|
| `Application/Auth/Commands/Login/LoginCommandHandler.cs` | Removida a checagem `Status == blocked` (mantida a checagem `Status == inactive`, fora de escopo) |
| `Application/Auth/Commands/RefreshToken/RefreshTokenCommand.cs` | Mesma remoção |
| `Application/Auth/Commands/GoogleLogin/GoogleLoginCommand.cs` | Mesma remoção |
| `Application/Bookings/Commands/CreateBooking/CreateBookingCommandHandler.cs` | Inalterado — segue sendo a única barreira contra `blocked` |
| `Application/Auth/Commands/Login/LoginCommand.cs` | `UserDto` ganha `Status` e `BlockedReason` (opcionais, mesmo padrão já usado pelo ERP em `ErpStudentsController`) |
| `Application/Auth/Commands/Login/LoginCommandHandler.cs` / `Application/Auth/Queries/GetMe/GetMeQuery.cs` | Preenchem os dois campos novos do `UserDto` (login e `GET /auth/me`) |

Conferido por grep: os únicos 4 pontos do código que checavam `Status == blocked` eram exatamente
os citados na demanda (`LoginCommandHandler`, `RefreshTokenCommand`, `GoogleLoginCommand`,
`CreateBookingCommandHandler`) — nenhum middleware/filtro global genérico encontrado.

## 3. Contrato de API que mudou

- **`POST /auth/login`**, **`POST /auth/refresh`**, **`POST /auth/google`**: aluno com
  `Status: "blocked"` (qualquer motivo) agora recebe token normalmente, em vez de `403`.
- **`UserDto`** (usado por `login`, `google-login` e `GET /auth/me`): dois campos novos e
  aditivos — `status` (`"active"` / `"blocked"` / `"inactive"`) e `blockedReason` (string ou
  `null`, ex. `"payment_failed"` quando o bloqueio foi automático por inadimplência).
- **`POST /api/bookings`**: sem mudança — `caller.Status == blocked` continua recusando com `403`.

## 4. Testes

`dotnet build` e `dotnet test` rodados; os 49 testes existentes continuam passando.
`Sinchrony.Tests/Unit/Commands/CreateBookingCommandTests.cs` continua não compilando por motivo
pré-existente e não relacionado (assinatura desatualizada de `CreateBookingCommand`/Handler — já
registrado em `2026-09-22-pagamentos-recorrentes-e-erros-asaas.md`).

## 5. Fora de escopo (fica com o app, por acordo do pedido)

- UI do App pro aviso de bloqueio + botão de atualizar cartão.
- Qualquer claim/token restrito — não é necessário, o controle continua todo no
  `CreateBookingCommandHandler`.
