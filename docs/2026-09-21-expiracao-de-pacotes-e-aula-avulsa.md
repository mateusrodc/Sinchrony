# Expiração de pacotes, fila e Aula Avulsa

Data: 21/09/2026 · Escopo: back-end (API) + banco · Status: implementado localmente, **ainda não commitado nem publicado**

## 1. Resumo

Investigando dois casos reais de alunas no ERP, encontramos que **nada no sistema expirava pacote** e que a **fila de pacotes nunca andava**. A partir disso:

1. Definimos a regra de negócio: **quando o pacote vence, os créditos restantes expiram junto** (ex.: pacote de 8 créditos com 30 dias de validade).
2. Criamos a expiração automática e a promoção da fila.
3. Corrigimos o extrato, que mostrava crédito que nunca entrou.
4. Criamos o conceito de **pacote avulso** (`IsSingleClass`), para que a Aula Avulsa não bloqueie nem enfileire a compra de um plano real.

## 2. O que aconteceu (incidentes)

### Caso 1: Essence "Na fila" e 8 créditos que não entraram

Vínculo manual do Essence (Mensal) para uma aluna que já tinha Aula Avulsa ativa.

- O Essence tem estratégia de compra `queue`. Com pacote ativo, o sistema cria o novo pacote como `queued` **com 0 crédito**.
- Mesmo assim, o `AssignPackage` (`ErpStudentsController`) gravava sempre a transação `+8`, com saldo depois = 0. O extrato mostrava `+8 → 0`, o que era enganoso.
- **Nada tirava o pacote da fila.** O único ponto que ativa pacote em fila era o webhook do Asaas (só PIX, e só sem pacote ativo). Vínculo manual e cartão nunca eram promovidos.
- **Nada expirava `StudentPackage`.** `Expire()` e `IsExpired()` existiam mas ninguém chamava. A Aula Avulsa (0 crédito) ficaria ativa para sempre e a fila nunca andaria.

### Caso 2: "Ajustar Créditos" e a vigência antiga

Ajuste de +4 créditos feito em 21/09, e o card mostrava a vigência 10/09–10/10.

- `POST /api/students/{id}/credits/adjust` só altera `User.Credits`. Não cria nem renova `StudentPackage`. O card mostra a vigência do pacote que já existia. Não era bug de gravação, era uma lacuna: crédito e pacote são desacoplados.
- Como a validade não era imposta em lugar nenhum, os créditos nunca venciam. Só o texto do card dizia que venciam.

### Caso 3 (risco descoberto): Aula Avulsa travando um plano real

Quem tem só Aula Avulsa e compra um Essence (`queue`) ficaria com o Essence "Na fila" por até 30 dias, mesmo tendo pago. A estratégia é avaliada pelo pacote **novo**, contra qualquer pacote ativo, sem olhar o tipo do ativo. Não existia nenhum campo para distinguir "avulso" de "plano real". `PackageType.Rank` só ordena a lista e não é usado em regra.

## 3. Regras de negócio novas

| Regra | Comportamento |
|---|---|
| Vencimento do pacote | Pacote `active` com `EndDate < agora` vira `expired`, as alocações são zeradas e **o saldo restante do aluno vai a 0** |
| Extrato | Expiração gera transação `package_expiration` (valor negativo, saldo depois = 0) |
| Fila | Ao expirar, o pacote `queued` mais antigo é ativado: vigência começa agora, cria alocação, soma os créditos e gera transação `queue_activation` |
| Pacote avulso | Se o pacote ativo é avulso e o novo **não** é, o novo **ativa na hora**, seja qual for a estratégia dele (`queue`, `block`...). A avulsa é `cancelled`. **O crédito que sobrou da avulsa é mantido** no saldo |
| Avulso + avulso | Segue a estratégia normal (`sum_credits`) |
| Plano + plano | Continua seguindo a estratégia normal (`queue` continua enfileirando) |
| Vínculo manual | O extrato só registra o que realmente entrou no saldo. Pacote que foi para a fila não gera transação de crédito |

### Casos especiais de PIX

O webhook do Asaas **já credita o saldo na confirmação**, mesmo quando o pacote fica na fila. Para não gerar erro:
- Fila com PIX **confirmado**: os créditos são preservados na expiração do pacote anterior, e **não** são creditados de novo na promoção.
- Fila com PIX **pendente**: não é promovido.
- O webhook também passou a respeitar a regra do avulso: se o ativo é avulso, cancela e ativa o plano pago.

## 4. Mudanças no código

| Arquivo | Mudança |
|---|---|
| `Infrastructure/Services/StudentPackageLifecycleService.cs` (novo) | Regra de expiração + promoção da fila |
| `Infrastructure/Services/PackageExpirationService.cs` (novo) | `BackgroundService`, roda a cada 1 min, um escopo/DbContext por pacote (falha em um não afeta os outros) |
| `Infrastructure/DependencyInjection.cs` | Registra os dois serviços |
| `Domain/Entities/Package.cs` | `IsSingleClass`, `SetSingleClass()`, `ReplacesActive()`, `GetCreditsPerPerson()` |
| `Domain/Entities/User.cs` | `ExpireCredits(keep)` |
| `Application/Payments/Commands/PurchasePackageService.cs` | `ProcessAsync` agora devolve `PackageGrantResult(StudentPackage, CreditsAdded)`; aplica a regra do avulso |
| `Application/Packages/Commands/PurchasePackage/PurchasePackageCommand.cs` | Estratégia `block` não bloqueia quando o ativo é avulso |
| `Api/Controllers/Erp/ErpStudentsController.cs` | `AssignPackage`: extrato só do que entrou + resposta com o pacote criado. `AdjustCredits`: campo opcional `extendDays` |
| `Api/Controllers/App/WebhooksController.cs` | Ativa o plano pago por cima do avulso |
| `Api/Controllers/Erp/ErpPackagesController.cs` + commands/DTO | `isSingleClass` em criar/editar/listar pacote |
| `Infrastructure/Migrations/20260921170000_AddPackageIsSingleClass*` | Coluna `packages."IsSingleClass" boolean NOT NULL DEFAULT false` |

Testes: 46 passando (novos: expiração, promoção da fila, PIX confirmado/pendente, regra do avulso, `ExpireCredits`).

## 5. Contratos de API que mudaram

Todas as mudanças são **aditivas**. O front atual continua funcionando.

- `POST /api/students/{id}/packages`: a resposta agora traz o pacote **criado** (`status: "queued"` quando foi para a fila). Antes devolvia o pacote ativo antigo.
- `POST /api/students/{id}/credits/adjust`: body aceita `extendDays` (int, opcional, > 0). Estende a validade do pacote ativo. Sem pacote ativo devolve erro `NO_ACTIVE_PACKAGE`. A resposta traz `packageEndDate`.
- `GET/POST/PUT /api/packages`: campo `isSingleClass`. **No `PUT`, se o campo não for enviado, o valor do banco é preservado.** Isso é intencional, para o front atual não zerar o valor definido direto no banco.
- Extrato (`GET /api/students/{id}/credit-transactions`): novos valores de `type`: `package_expiration` e `queue_activation`.

### Pendências de front

- Checkbox "Pacote avulso (não bloqueia outros planos)" na tela de pacote, enviando `isSingleClass`.
- Campo opcional "Estender validade (dias)" em "Ajustar Créditos".
- Exibir os dois novos tipos de transação no extrato.

## 6. Como publicar (ordem importa)

1. **Prévia de impacto (antes do deploy).** No primeiro ciclo, todo pacote que já estava vencido será expirado de uma vez e o saldo dessas alunas será zerado. Veja quem é afetada:
   ```sql
   SELECT u."Name", u."Email", u."Credits", sp."EndDate"
   FROM student_packages sp JOIN users u ON u."Id" = sp."StudentId"
   WHERE sp."Status" = 'active' AND sp."EndDate" < now() AND u."Credits" > 0
   ORDER BY sp."EndDate";
   ```
   Note: `Status` é texto (`'active'`, `'queued'`, `'expired'`, `'cancelled'`), não número. Se alguém não deve perder saldo, estenda a validade antes (`extendDays` no "Ajustar Créditos").
2. **Deploy.** O `Program.cs` roda `db.Database.Migrate()` na subida, então a coluna é criada sozinha. **Não rodar o `ALTER TABLE` manualmente antes**, senão a migration falha por coluna duplicada e a API não sobe (5 tentativas e aborta).
   - Para subir com a expiração desligada: variável de ambiente `PackageExpiration__Enabled=false`.
3. **Marcar a Aula Avulsa como avulsa** (só depois do deploy):
   ```sql
   UPDATE packages SET "IsSingleClass" = true WHERE "Name" ILIKE 'Aula Avulsa%';
   SELECT "Id", "Name", "IsSingleClass" FROM packages ORDER BY "Name";
   ```
   Antes disso o comportamento é idêntico ao antigo. Se houver outro pacote avulso com nome diferente, marcar também.
4. **Ligar a expiração** (se subiu desligada), removendo a variável ou colocando `true`.

## 7. Correção pontual da aluna do Caso 1

Só tirar o Essence da fila. Os 8 créditos já foram lançados manualmente pelo "Ajustar Créditos", então o script **não mexe em créditos**. A Aula Avulsa é cancelada porque o sistema assume um único pacote ativo por aluna (`GetActiveByStudentAsync` pega o primeiro `active`).

```sql
DO $$
DECLARE
  v_email  text := 'EMAIL_DA_ALUNA';  -- preencher
  v_user uuid; v_active uuid; v_queued uuid; v_days int;
BEGIN
  SELECT "Id" INTO STRICT v_user FROM users WHERE "Email" = lower(v_email);
  SELECT "Id" INTO STRICT v_active FROM student_packages
    WHERE "StudentId" = v_user AND "Status" = 'active';
  SELECT sp."Id", pk."ValidityDays" INTO STRICT v_queued, v_days
    FROM student_packages sp JOIN packages pk ON pk."Id" = sp."PackageId"
    WHERE sp."StudentId" = v_user AND sp."Status" = 'queued';

  UPDATE student_packages SET "Status" = 'cancelled' WHERE "Id" = v_active;

  UPDATE student_packages
     SET "Status" = 'active',
         "StartDate" = now(),
         "EndDate" = now() + make_interval(days => v_days)
   WHERE "Id" = v_queued;
END $$;
```

Se a aluna não tiver exatamente 1 pacote ativo e 1 na fila, o bloco aborta sem alterar nada (`STRICT`). Depois do deploy, esses 8 créditos passam a vencer junto com o Essence (30 dias a partir da execução).

## 8. Pontos de atenção e dívidas conhecidas

- **Expiração é irreversível para o saldo.** Por isso o kill switch e a prévia do passo 1.
- **Saldo é global** (`User.Credits`); ao expirar o pacote ativo, **todo** o saldo é zerado, exceto o que já foi pago por pacote PIX na fila. Créditos ajustados manualmente também expiram.
- **Alocações (`dependent_package_allocations`)** não são debitadas nas reservas (a reserva debita só `User.Credits`), então não são fonte confiável de saldo.
- **`sum_validity`** só estende a validade e não soma créditos. Não alterei, mas vale confirmar se é a intenção do negócio.
- **`ProcessAndReturnAsync`** (compra com cartão) ainda devolve o pacote ativo, não o criado, quando o novo vai para a fila. Impacto só na resposta.
- **O webhook do PIX não cria alocações** ao ativar um pacote (comportamento anterior, mantido).
- **`Sinchrony.Tests/Unit/Commands/CreateBookingCommandTests.cs` não compila** (construtor e `CreateBookingCommand` desatualizados). É anterior a estas mudanças. Por isso o `dotnet test` da solução falha. Rodei os testes com esse arquivo excluído só na execução.
- Não usei `dotnet ef migrations add` de propósito: ele executa o startup da API, que aplica migrations no banco da connection string. A migration, a Designer e o snapshot foram escritos à mão seguindo o padrão das existentes. Vale conferir com `dotnet ef migrations has-pending-model-changes` em um ambiente seguro.
