# Recorrência V2 — renovação automática cobrada pelo Sinchrony (sem assinatura Asaas)

Data: 23/09/2026 · Escopo: back-end (API) + banco · Status: implementado localmente, ainda não
commitado nem publicado

## 1. Resumo

Substitui por completo a implementação anterior do mesmo dia (`POST /v3/subscriptions` da Asaas),
que nunca chegou a ser commitada. Decisão da cliente (23/09): a Asaas só cobra em ciclos de
calendário fixos (`MONTHLY`, `QUARTERLY`...), o que não bate com planos de validade arbitrária
(30, 45, 90, 180 dias). O Sinchrony passa a cobrar o cartão salvo do aluno diretamente
(`ChargeCardAsync`), no vencimento exato de `Package.ValidityDays`, com retentativas e bloqueio
próprios.

Implementa `DEMANDA_RECORRENCIA_COMPRA_E_CANCELAMENTO_BACKEND_V2.md` e
`ESPECIFICACAO_API_STATUS_ASSINATURAS_V2.md` juntas — a segunda é, na prática, a mesma spec de
antes com o "quem escreve o status" trocado de webhook/reconciliação de assinatura pro job de
renovação.

## 2. Modelo

**`StudentPackage`** ganha, além dos campos de status já existentes (`PaymentStatus`,
`LastPaidAt/Amount`, `ProblemSince`, `LastFailureReason`, `LastSyncedAt`):
- `AutoRenew` (bool) — liga na contratação (`EnableAutoRenew`), desliga no cancelamento
  (`Cancel()` ou `CancelRenewal()`).
- `RenewalCardId` (FK `Card`, nullable, `SetNull` on delete) — cartão usado nas renovações.
- `RenewalAttempts` / `NextRenewalAttemptAt` — controlam as retentativas.

`AsaasSubscriptionId` continua na tabela (não usado em contratações novas) só como rede de
segurança caso sobre algum registro legado — nada no código atual cria ou consulta assinaturas na
Asaas.

**`Purchase`** ganha `Kind` (`"purchase"` | `"renewal"`) — cada tentativa de cobrança de
renovação gera uma `Purchase` própria, ligada ao `StudentPackageId`.

**`nextDueDate` nas respostas de API não é mais um valor armazenado** — é sempre `EndDate`
convertido pra horário de Brasília (`Application/Common/BrasiliaTime.cs`), calculado na hora.
`amount` é sempre `Package.Price` (preço atual), não um snapshot.

## 3. Fluxo

1. **Contratação** (`PurchasePackageCommand`, ramo `card` com `Package.IsRecurring`): cobra
   `package.Price` (nunca `request.Amount`), localiza o `Card` pelo token, e — aprovado na hora
   ou fila aguardando webhook, igual a qualquer compra de cartão — chama
   `EnableAutoRenew(cardId)`. Não existe mais um ramo `IsRecurring` separado: a mecânica é
   idêntica à de comprar com cartão avulso.
2. **`RecurringRenewalJob`** (`BackgroundService`, a cada 5min, um escopo por pacote — mesmo
   molde do `PackageExpirationService`): cobra todo `StudentPackage` com `AutoRenew=true` que
   esteja a até 24h do vencimento (ou com `NextRenewalAttemptAt` no passado), e reconcilia
   renovações `pending` com mais de 10 minutos (webhook provavelmente perdido) consultando
   `GET /v3/payments/{id}`.
3. **`RecurringRenewalService`** é o único lugar que sabe aplicar sucesso/falha de uma renovação
   — usado pelo job, pelo webhook (`Purchase.Kind == "renewal"` confirmada/recusada) e pelo
   `/sync` manual:
   - Sucesso: credita, `ChargeRenewed()` (novo ciclo começa no `EndDate` anterior, não em "agora"
     — diferença chave do `RenewCycle()` legado), `MarkUpToDate`, reativa se bloqueado por
     `payment_failed`.
   - Falha: `RecordRenewalFailure` (retentativas em `ProblemSince + 1 dia` e `+ 3 dias`); na 3ª
     tentativa, `SubscriptionOverdueService.BlockAndAlertAsync` bloqueia o aluno e dispara o
     alerta de admin (e-mail + `AdminAlert` persistido, idempotente por transactionId).
4. **Cancelamento da renovação** (`POST /students/me/subscription/cancel`): `CancelRenewal()` —
   `AutoRenew=false`, o pacote continua válido normalmente até o `EndDate`, só não gera o próximo
   ciclo.
5. **Troca de cartão** (`PATCH /students/me/subscription/card`): grava `RenewalCardId`; se estava
   `retrying`/`overdue`, marca `NextRenewalAttemptAt = now` pro job tentar no próximo tick (até
   5min depois, não sincronamente no PATCH).

## 4. Mudanças no código

| Área | Arquivo | Mudança |
|---|---|---|
| Domínio | `StudentPackage.cs` | `AutoRenew`/`RenewalCardId`/`RenewalAttempts`/`NextRenewalAttemptAt`; `EnableAutoRenew`/`SetRenewalCard`/`CancelRenewal`/`ChargeRenewed`/`RecordRenewalFailure` novos; `Cancel()` zera `AutoRenew`; removidos `NextDueDate`/`NextDueAmount`/`MarkNextDue` |
| Domínio | `Purchase.cs` | `Kind` + `CreateRenewalPending` |
| Aplicação | `Subscriptions/SubscriptionOverdueService.cs` | Simplificado pra só `BlockAndAlertAsync` (bloqueio+alerta) — removido o caminho legado de webhook de assinatura |
| Aplicação | `Common/BrasiliaTime.cs` (novo) | Conversão UTC→Brasília compartilhada |
| Aplicação | `PurchasePackageCommand.cs` | Ramo `IsRecurring` fundido no ramo `card` |
| Aplicação | `UpdateSubscriptionCardCommand.cs` / `CancelSubscriptionCommand.cs` | Reescritos pro modelo local (`SetRenewalCard`/`CancelRenewal`), sem chamada à Asaas (exceto guarda legada se `AsaasSubscriptionId` ainda estiver setado) |
| Infraestrutura | `AsaasService.cs`/`IAsaasService.cs` | Removidos `CreateSubscriptionAsync`/`GetSubscriptionAsync`/`GetLatestOverduePaymentAsync`; novo `GetPaymentAsync` |
| Infraestrutura | `RecurringRenewalService.cs` / `RecurringRenewalJob.cs` (novos) | Substituem `SubscriptionReconciliationService`/`Job` (deletados) |
| Infraestrutura | `StudentPackageLifecycleService.cs` | Exclusão de expiração agora considera `AutoRenew` além do legado `AsaasSubscriptionId` |
| API | `WebhooksController.cs` | Removida toda a máquina de webhook de assinatura Asaas; `Purchase.Kind == "renewal"` roteia pro `RecurringRenewalService` |
| API | `StudentPackageController.cs` / `ErpSubscriptionsController.cs` | DTOs usam `Package.Price`/`BrasiliaTime.ToDate(EndDate)` em vez de campos armazenados; `GET /students/me/package` ganha `autoRenew`/`paymentStatus`/`nextDueDate` |
| API | `PayWithCardCommand.cs`/`PayWithPixCommand.cs` | Mensagem de erro atualizada ("renovação automática" em vez de "assinatura mensal") |
| Repositórios | `IStudentPackageRepository` | `ListActiveSubscriptionIdsAsync` → `ListDueForRenewalAsync` (nova regra de seleção); predicados de "isso é recorrente" viram `AutoRenew \|\| AsaasSubscriptionId != null` |
| Repositórios | `IPurchaseRepository` | `HasActiveRenewalForCycleAsync`, `ListStalePendingRenewalsAsync`, `GetPendingRenewalAsync`, `GetByIdAsync` |
| Repositórios | `ICardRepository` | `GetByTokenAsync` |
| Banco | Mesma migration de hoje (`20260923120000_AddSubscriptionStatusAndAlerts`) **editada** em vez de empilhar outra — trocou `NextDueDate`/`NextDueAmount` por `AutoRenew`/`RenewalCardId`/`RenewalAttempts`/`NextRenewalAttemptAt`, `purchases` ganhou `Kind` |
| DI | `DependencyInjection.cs` | `RecurringRenewalService`/`RecurringRenewalJob` no lugar dos serviços de reconciliação |

## 5. Decisão registrada em código (spec ambígua)

`ListDueForRenewalAsync`: a V2 pede que a seleção automática do job **exclua** pacotes `overdue`
(§3.4), mas também pede que trocar o cartão de um pacote `overdue` faça o job "tentar na hora"
(§4.3) — as duas frases se contradizem se lidas ao pé da letra. Resolvido assim: `overdue` só
entra na seleção quando `NextRenewalAttemptAt` foi setado explicitamente (pela troca de cartão);
o gatilho automático de `EndDate - 24h` nunca pega um pacote `overdue`. Comentado no código
(`StudentPackageRepository.ListDueForRenewalAsync`).

## 6. Contrato de API que mudou (em relação à V1 de hoje, nunca publicada)

- `POST /packages/{id}/purchase` com plano recorrente: mesmo contrato de compra por cartão avulso
  (sem campo de assinatura no retorno).
- `GET /students/me/package`: novos campos `autoRenew`, `paymentStatus`, `nextDueDate`.
- `POST /students/me/subscription/cancel`: não cancela o pacote imediatamente — só desliga a
  renovação; o pacote segue válido até o `EndDate`.
- Os 7 endpoints de `ESPECIFICACAO_API_STATUS_ASSINATURAS_V2.md` §4 (inalterados na V2) seguem
  valendo com o mesmo contrato de resposta — só a fonte dos dados mudou.

## 7. Testes

`dotnet build` e `dotnet test` rodados a cada etapa da migração de V1→V2; os 49 testes existentes
continuam passando. `CreateBookingCommandTests.cs` segue com o problema de compilação
pré-existente e não relacionado, já registrado nos docs anteriores.

Não escrevi testes novos pra `RecurringRenewalService`/`StudentPackage.Charge*`/`Record*` nesta
rodada — dado o volume de mudança em pouco tempo, ficou pra uma revisão seguinte. Os candidatos
mais valiosos: `ChargeRenewed()` encadeando a partir do `EndDate` antigo (não de `UtcNow`),
`RecordRenewalFailure`'s cálculo de `NextRenewalAttemptAt` a partir de `ProblemSince`, e
`ListDueForRenewalAsync`'s regra de seleção (a decisão registrada na seção 5 acima).

## 8. Pontos de atenção e dívidas conhecidas

- **Sem ambiente de sandbox da Asaas nesta sessão** — nenhum dos critérios de aceite das duas
  demandas foi validado contra a Asaas de verdade. Validar em ambiente de teste antes de publicar.
- **`payments[].dueDate`** continua sendo uma aproximação (data de criação da `Purchase`, não uma
  data de vencimento real armazenada) — mesma ressalva já registrada antes.
- **`RecurringRenewalJob` sem lock distribuído** — se a API rodar em mais de uma instância, cada
  uma processa a lista de pacotes devidos de forma independente. Não duplica cobrança de verdade
  graças ao `HasActiveRenewalForCycleAsync`/dedup por `TransactionId`, mas gera chamadas
  redundantes à Asaas em deploys multi-instância.
- **Créditos de renovação** usam `Package.GetCreditsToGrant()` — mesma regra já usada na
  contratação e no legado, considerando família/dependentes.
