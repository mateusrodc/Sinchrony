# Erros mascarados da Asaas + Pagamentos Recorrentes com Bloqueio Automático

Data: 22/09/2026 · Escopo: back-end (API) + banco · Status: implementado localmente, **ainda não commitado nem publicado**

## 1. Resumo

Duas demandas implementadas em sequência, porque a segunda depende tecnicamente da primeira:

1. **`DEMANDA_ASAAS_ERROS_MASCARADOS_E_STATUS_CARTAO_BACKEND.md`** — erros da Asaas (chave inválida, CPF inválido, cartão recusado etc.) paravam de virar 500 genérico, e pagamento no cartão passou a respeitar o `status` retornado (`PENDING` = aguardando análise antifraude) em vez de creditar sempre de forma otimista.
2. **`PLANEJAMENTO_PAGAMENTOS_RECORRENTES.md` (V2)** — pacotes recorrentes agora podem ser cobrados via assinatura da Asaas (`POST /subscriptions`), com bloqueio automático do aluno quando a cobrança falha definitivamente (`PAYMENT_OVERDUE`) e desbloqueio automático quando a Asaas confirma o próximo pagamento.

Nenhuma tela foi construída — as duas demandas documentam que o frontend (ERP + App) fica por conta do outro dev, a partir dos contratos de API descritos abaixo. O toggle manual de bloqueio/desbloqueio do aluno **já existia** e não precisou de nenhuma mudança.

## 2. Regras de negócio novas

| Área | Regra |
|---|---|
| Erros da Asaas | Qualquer rejeição da Asaas (`GetOrCreateCustomerAsync`, `CreatePixChargeAsync`, `ChargeCardAsync`) vira `422` com a mensagem real da Asaas, em vez de `500 INTERNAL_ERROR` |
| Cartão assíncrono | `ChargeCardAsync` lê o `status` da resposta. Se vier `PENDING` (análise antifraude), a compra fica `pending` e **não credita** — só credita quando o webhook confirmar (`PAYMENT_CONFIRMED`/`PAYMENT_RECEIVED`) |
| Cartão reprovado depois de aprovado | `PAYMENT_REPROVED_BY_RISK_ANALYSIS` / `PAYMENT_CREDIT_CARD_CAPTURE_REFUSED` numa compra avulsa: se créditos/pacote já tinham sido concedidos, são revertidos (`Purchase` vira `failed`) |
| Assinatura recorrente | Pacote com `IsRecurring = true` só aceita cartão. Na compra, cria a assinatura na Asaas (`POST /subscriptions`, ciclo `MONTHLY`) e o `StudentPackage` fica `queued` até o primeiro pagamento confirmar |
| Tolerância antes de bloquear | Nenhuma lógica própria — usa o reprocessamento nativo da Asaas. Só bloqueia no webhook `PAYMENT_OVERDUE` (ela desistiu de tentar) |
| Créditos ao bloquear | Ficam intactos. O bloqueio (`User.Status = blocked`, `BlockedReason = "payment_failed"`) trava só login/agendamento, igual já acontecia com bloqueio manual |
| Desbloqueio | Automático: quando a mesma assinatura confirma um pagamento (`PAYMENT_CONFIRMED`/`PAYMENT_RECEIVED`) enquanto o aluno está bloqueado por `payment_failed`, o backend chama `Reactivate()` sozinho |
| Aviso preventivo | `PAYMENT_CREDIT_CARD_CAPTURE_REFUSED` numa assinatura dispara e-mail de aviso, **sem bloquear** (assumimos esse evento como o "primeiro sinal de falha" citado no planejamento — não há um nome de evento explícito da Asaas pra isso; vale confirmar com a Asaas/cliente se é o esperado) |

## 3. Mudanças no código

| Arquivo | Mudança |
|---|---|
| `Infrastructure/Services/AsaasService.cs` | `GetOrCreateCustomerAsync`/`CreatePixChargeAsync`/`ChargeCardAsync` lançam `DomainException.Validation` com a descrição real da Asaas em vez de `InvalidOperationException`. `ChargeCardAsync` retorna `Status`. Novos métodos `CreateSubscriptionAsync` e `UpdateSubscriptionCardAsync` |
| `Domain/Interfaces/Services/IAsaasService.cs` | `CardPaymentResult` ganha `Status`; novo `SubscriptionResult`; assinaturas dos dois métodos novos |
| `Domain/Entities/Purchase.cs` | Novo método `Fail()` (`Status = "failed"`) |
| `Domain/Entities/Package.cs` | Novo campo `IsRecurring` + `SetRecurring()` |
| `Domain/Entities/StudentPackage.cs` | Novo campo `AsaasSubscriptionId` + `SetAsaasSubscriptionId()` + `RenewCycle()` (renova vigência de um ciclo já ativo, sem recriar o registro) |
| `Domain/Entities/User.cs` | Novo campo `BlockedReason`; `Block()` aceita motivo opcional; `Reactivate()` limpa o motivo |
| `Domain/Interfaces/Repositories/IStudentPackageRepository.cs` + `Infrastructure/.../StudentPackageRepository.cs` | Novo `GetByAsaasSubscriptionIdAsync` |
| `Application/Payments/Commands/PayWithCard/PayWithCardCommand.cs` | Só credita/confirma se `Status` for `CONFIRMED`/`RECEIVED`; senão cria `Purchase` pendente |
| `Application/Packages/Commands/PurchasePackage/PurchasePackageCommand.cs` | Mesma checagem de `Status` pro fluxo de compra avulsa; novo branch pra `Package.IsRecurring` (cria assinatura, `StudentPackage` fica `queued`) |
| `Application/Packages/Commands/UpdateSubscriptionCard/UpdateSubscriptionCardCommand.cs` (novo) | Aluno troca o cartão de uma assinatura ativa, reaproveitando cartão já tokenizado (`Card.Token`) |
| `Application/Packages/Queries/{CreatePackage,UpdatePackage,ListPackages}*.cs` | Campo `isRecurring` em criar/editar/listar pacote (mesmo padrão do `isSingleClass` existente) |
| `Api/Controllers/App/WebhooksController.cs` | Reescrito: dispatch por evento (`PAYMENT_CONFIRMED`/`RECEIVED`, `PAYMENT_OVERDUE`, `PAYMENT_REPROVED_BY_RISK_ANALYSIS`, `PAYMENT_CREDIT_CARD_CAPTURE_REFUSED`), separando fluxo de assinatura (por `payment.subscription`) do fluxo avulso (por `Purchase.TransactionId`) já existente. Envia e-mails via `IEmailService` |
| `Api/Controllers/App/StudentPackageController.cs` | Novo `PATCH students/me/subscription/card` |
| `Api/Controllers/Erp/ErpStudentsController.cs` | `blockedReason` no retorno de aluno |
| `Api/Controllers/Erp/ErpPurchasesController.cs` | Status `"failed"` aceito no filtro; `isRecurring` no retorno |
| `Api/Controllers/Erp/ErpPackagesController.cs` | `isRecurring` em criar/editar pacote |
| `Application/Purchases/Queries/ListPurchases/ListPurchasesQuery.cs` | `isRecurring` no `PurchaseDto` (App) |
| `Infrastructure/Persistence/Configurations/{Package,StudentPackage,User}Configuration.cs` | Mapeamento dos 3 campos novos |
| `Infrastructure/Migrations/20260922120000_AddRecurringPaymentsAndBlockReason*` | `packages."IsRecurring" boolean NOT NULL DEFAULT false`, `student_packages."AsaasSubscriptionId" varchar(50)` + índice, `users."BlockedReason" varchar(50)` |

Testes: os 49 testes existentes continuam passando (rodados com `CreateBookingCommandTests.cs` excluído — ver seção 6).

## 4. Contratos de API que mudaram

Todas as mudanças são **aditivas**.

- **`POST /api/payments/card`** e **`POST /api/packages/purchase`** (compra com cartão): a resposta pode vir com `success: false` quando o pagamento fica `PENDING` (antes sempre voltava `true`, mesmo sem confirmação). Isso é o comportamento correto — antes o front achava que tinha dado certo mesmo sem confirmação de risco.
- **`POST /api/packages/purchase`** com pacote `isRecurring = true`: só aceita `paymentMethod: "card"` (erro `RECURRING_REQUIRES_CARD` senão). A resposta volta com `status: "queued"` até o primeiro ciclo confirmar via webhook.
- **`GET/POST/PUT /api/packages`**: novo campo `isRecurring` (mesmo comportamento aditivo do `isSingleClass` — se não enviado no `PUT`, mantém o valor do banco).
- **`GET /api/purchases`** (ERP): `status` aceita `"failed"`; resposta ganha `isRecurring` (booleano, derivado do pacote da compra).
- **`GET /api/purchases`** (App, `ListPurchasesQuery`): resposta ganha `isRecurring`.
- **`GET /api/students/{id}`**: resposta ganha `blockedReason` (`"payment_failed"` quando o bloqueio foi automático por inadimplência; `null` quando é bloqueio manual do admin ou aluno ativo).
- **Novo endpoint `PATCH /students/me/subscription/card`** (App, autenticado): body `{ cardId: guid }`. Troca o cartão usado pela assinatura recorrente ativa/na fila do aluno logado. **Não desbloqueia na hora** — o desbloqueio só acontece quando a Asaas confirmar a próxima cobrança com o novo cartão.
- **Erros da Asaas**: qualquer chamada que dependa de `AsaasService` (compra PIX, compra cartão, criar assinatura) agora pode devolver `422` com `code` específico (`ASAAS_CUSTOMER_ERROR`, `ASAAS_PIX_ERROR`, `ASAAS_CARD_CHARGE_ERROR`, `ASAAS_SUBSCRIPTION_ERROR`, `ASAAS_SUBSCRIPTION_CARD_UPDATE_ERROR`) e mensagem real, em vez de `500`.

### Webhook da Asaas (`POST /webhooks/asaas`)

Nenhuma mudança de contrato pro chamador (a Asaas), só de comportamento interno:

- `PAYMENT_CONFIRMED` / `PAYMENT_RECEIVED` com `payment.subscription` presente → fluxo de assinatura (credita, renova vigência, desbloqueia se aplicável). Sem `subscription` → fluxo avulso de sempre (PIX/cartão único).
- `PAYMENT_OVERDUE` com `payment.subscription` → bloqueia o aluno (`payment_failed`) + registra `Purchase` `failed` + e-mail.
- `PAYMENT_REPROVED_BY_RISK_ANALYSIS` / `PAYMENT_CREDIT_CARD_CAPTURE_REFUSED`: com `subscription` → e-mail preventivo, sem bloquear; sem `subscription` → reverte crédito/pacote de uma compra avulsa que já tinha sido confirmada.

### Pendências de front (o que falta pro outro dev construir)

Repetindo o que já está no planejamento, pra não se perder:

**ERP**
- Checkbox "Pacote recorrente" na tela de pacote, enviando `isRecurring`.
- Compras de Pacote: filtro de status ganha "Falhou" (badge própria).
- Ficha do aluno: quando `blockedReason === "payment_failed"`, mostrar aviso perto do toggle "Bloqueado" já existente (ex.: "Bloqueado por falha de pagamento em DD/MM — assinatura: Essence (Recorrente)"). O toggle em si não muda.

**App**
- Banner de bloqueio pro aluno (tela do aluno): hoje só dá erro genérico ao logar/reservar bloqueado. Precisa de tela/mensagem explicando o motivo com botão de ação direta pra `PATCH /students/me/subscription/card`.
- Aviso não-bloqueante quando a cobrança falha mas ainda não foi bloqueado (evento de captura recusada, ver ressalva da seção 2).

## 5. Como publicar (ordem importa)

1. **Migration escrita à mão** (`20260922120000_AddRecurringPaymentsAndBlockReason.cs` + `.Designer.cs` + `ApplicationDbContextModelSnapshot.cs` atualizado), seguindo o mesmo padrão já usado no repo (`20260921170000_AddPackageIsSingleClass`). **Não rodei `dotnet ef migrations add`** de propósito — ele sobe o host da API e pode aplicar migration direto no banco da connection string configurada. Vale conferir com `dotnet ef migrations has-pending-model-changes` num ambiente seguro antes de aplicar.
2. **Deploy.** `Program.cs` roda `db.Database.Migrate()` na subida — as 3 colunas novas (`packages."IsRecurring"`, `student_packages."AsaasSubscriptionId"`, `users."BlockedReason"`) são todas nullable/com default, então não há impacto em dado existente.
3. **Configuração da Asaas.** Confirmar que o token do webhook (`Asaas:WebhookToken`) está configurado no ambiente de produção — ele já era exigido antes, não mudou aqui. Os eventos novos que o webhook passa a tratar (`PAYMENT_OVERDUE`, `PAYMENT_REPROVED_BY_RISK_ANALYSIS`, `PAYMENT_CREDIT_CARD_CAPTURE_REFUSED`) precisam estar habilitados na configuração de webhook do painel da Asaas, senão nunca chegam.
4. **Marcar os pacotes recorrentes existentes** (se já houver algum pacote "(Recorrente)" só por nome, sem o flag novo):
   ```sql
   UPDATE packages SET "IsRecurring" = true WHERE "Name" ILIKE '%recorrente%';
   SELECT "Id", "Name", "IsRecurring", "AllowsCard" FROM packages ORDER BY "Name";
   ```
   Atenção: pacote marcado como `IsRecurring = true` só aceita compra por cartão (`AllowsCard` precisa ser `true`).

## 6. Pontos de atenção e dívidas conhecidas

- **Evento de "primeira falha, ainda não bloqueado"**: o planejamento pede um e-mail preventivo antes do `OVERDUE`, mas não nomeia o evento exato da Asaas. Usei `PAYMENT_CREDIT_CARD_CAPTURE_REFUSED` como proxy — **confirmar com a documentação/suporte da Asaas** se esse é de fato o evento disparado durante o reprocessamento automático de uma assinatura, ou se é outro (ex.: algum evento de `PAYMENT_UPDATED`).
- **Renovação de ciclo (`RenewCycle`)** reseta `StartDate`/`EndDate` a partir de agora, sem lógica de proration — cada ciclo pago dá exatamente `Package.ValidityDays` de vigência, igual a uma compra nova.
- **Créditos de assinatura** usam `Package.GetCreditsToGrant()` (considera família/dependentes), diferente do fluxo avulso do webhook (`Purchase.Package?.Credits`, sem considerar família) — isso já era assim antes da minha mudança no fluxo avulso; não alterei esse comportamento por estar fora do escopo das duas demandas, só registrando a inconsistência.
- **Cancelamento de assinatura** (aluno ou admin cancelar de vez) é explicitamente fora de escopo nas duas demandas — precisa de uma demanda própria.
- **`Sinchrony.Tests/Unit/Commands/CreateBookingCommandTests.cs` não compila** (assinatura de `CreateBookingCommandHandler`/`CreateBookingCommand` desatualizada). **Confirmado anterior a estas mudanças** (reproduzido com `git stash -u` antes de qualquer edição). Por isso `dotnet test` da solução falha; os 49 testes foram rodados com esse arquivo excluído.
- Não rodei `dotnet ef migrations add` pelo mesmo motivo já registrado no doc anterior (`2026-09-21-expiracao-de-pacotes-e-aula-avulsa.md`): evita aplicar migration sem querer no banco da connection string configurada.
