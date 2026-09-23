# Renovação automática expira os créditos do ciclo anterior (Termos de Uso 6.3)

Data: 23/09/2026 · Escopo: back-end (API) + banco · Status: implementado localmente, ainda não
commitado nem publicado

## 1. Resumo

`DEMANDA_RENOVACAO_CREDITOS_EXPIRAM_BACKEND.md`: a renovação automática (commit `cf4a5d3`)
acumulava créditos por cima do saldo a cada ciclo pago e nunca recriava as cotas de dependentes —
contraria a cláusula 6.3 dos Termos de Uso (créditos não usados expiram, sem reaproveitamento),
que a cliente decidiu (23/09) que vale por ciclo também num plano recorrente.

A correção reaproveita a mecânica que já existia pra pacote avulso vencendo
(`StudentPackageLifecycleService.ExpireAsync`/`PromoteAsync`: zera saldo/cotas, credita o próximo)
para o caso "o próximo ciclo é o mesmo `StudentPackage`" em vez de reimplementar do zero.

## 2. O que muda, na prática

- **Cobrança confirmada antes do `EndDate`** (caso normal — o job cobra até 24h antes do
  vencimento): só registra o pagamento (`MarkUpToDate`, `Purchase` confirmada,
  `StudentPackage.MarkRenewalPaidForCycle()`). Créditos/cotas continuam intactos até o `EndDate`
  de verdade chegar — senão o aluno perderia o resto do ciclo que já pagou.
- **Virada do ciclo** (quando o `EndDate` chega e a renovação está paga) —
  `PackageExpirationService`, que já roda a cada minuto, processa em vez de expirar o pacote:
  1. zera o saldo que sobrou (`CreditTransaction` negativa) e debita as cotas antigas;
  2. `ChargeRenewed()` (novo `StartDate`/`EndDate`, volta pra `Status = active`);
  3. credita os créditos do ciclo novo (`CreditTransaction` positiva) e recria as cotas do
     titular e de cada dependente ativo.
- **Cobrança confirmada depois do `EndDate`** (retentativa, regularização de `overdue`,
  antifraude demorado): a virada acontece na hora da confirmação, mesmos 3 passos — não espera o
  job.

## 3. Mudanças no código

| Arquivo | Mudança |
|---|---|
| `Domain/Entities/StudentPackage.cs` | `RenewalPaidForCycle` (bool) + `MarkRenewalPaidForCycle()`; `ChargeRenewed()` zera o campo junto com `RenewalAttempts`/`NextRenewalAttemptAt` |
| `Infrastructure/Services/StudentPackageLifecycleService.cs` | Extraídos `ExpireCycleCredits`/`GrantCycleCreditsAsync` (usados por `ExpireAsync`, `PromoteAsync` e o novo fluxo); novos `TurnoverAsync` (só muta, quem chama salva), `TurnoverRenewalAsync` (carrega + salva, usado pelo job), `ListDueForTurnoverAsync`; `ListExpiredIdsAsync`/`ExpireAsync` ganham `&& !RenewalPaidForCycle` (pacote com renovação paga vai pro caminho novo, não expira) |
| `Infrastructure/Services/PackageExpirationService.cs` | Segunda passada por tick: processa `ListDueForTurnoverAsync` chamando `TurnoverRenewalAsync`, mesmo padrão de escopo-por-item da passada de expiração |
| `Infrastructure/Services/RecurringRenewalService.cs` | `ApplyRenewalSuccessAsync` bifurca em `now >= sp.EndDate` (chama `TurnoverAsync` na hora) vs `now < sp.EndDate` (`MarkRenewalPaidForCycle`); removido o crédito direto que ignorava dependentes; `ICreditTransactionRepository` saiu do construtor (sem uso) |
| `Infrastructure/Persistence/Repositories/StudentPackageRepository.cs` | `ListDueForRenewalAsync` ganha `&& !RenewalPaidForCycle` (otimização — evita tentar cobrar um ciclo que já está pago, embora o dedup por `Purchase` já protegesse) |
| `Infrastructure/Persistence/Configurations/StudentPackageConfiguration.cs` | Mapeia `RenewalPaidForCycle` |
| `Infrastructure/Migrations/20260923140000_AddRenewalPaidForCycle.*` | Nova migration (coluna `boolean not null default false`) — não editei a `20260923120000` porque ela já está commitada há duas rodadas e pode já ter rodado contra um banco real |

**Verificado, sem mudança de código:** prorrogação por atestado (`ExtendValidity`) — a janela de
cobrança e a janela de virada sempre leem o `EndDate` atual do banco, nunca um valor cacheado, então
empurrar o `EndDate` automaticamente empurra as duas junto.

## 4. `dependent_package_allocations` — cuidado ao reaproveitar `GrantCycleCreditsAsync`

Diferente de `PromoteAsync` (sempre lida com um `StudentPackage` novo, sem alocação prévia), a
virada de ciclo reaproveita o **mesmo** `StudentPackage` a cada renovação. `GrantCycleCreditsAsync`
remove as alocações antigas do pacote antes de recriar — sem isso, cada ciclo empilharia uma linha
de cota nova por dependente (as antigas ficando zeradas pra sempre), duplicando no
`GET /students/me/package` e fazendo `GetByStudentPackageAndDependentAsync` (pega a primeira linha
que encontrar) arriscar devolver uma cota velha em vez da do ciclo atual.

## 5. Testes

`dotnet build`/`dotnet test`: 0 erros, 77/77 passando (69 anteriores + 8 novos, todos os 4
critérios de aceite da demanda cobertos em `StudentPackageLifecycleServiceTests.cs`, seguindo o
padrão EF Core InMemory já usado no arquivo):

1. `TurnoverRenewalAsync_CreditsDoNotAccumulate_OldBalanceExpiresBeforeNewGrant` — 8 créditos,
   usou 5, saldo 3 → depois da virada, saldo 8 (não 11), transações −3 e +8.
2. `TurnoverRenewalAsync_FamilyPackage_AllocationsResetToFullPerPersonAndDoNotDuplicate` — titular
   + 1 dependente, 4 créditos cada, dependente zerado → depois da virada, as duas cotas voltam
   pra 4, sem duplicar linha.
3. `TurnoverAsync_CalledDirectly_ProducesSameResultAsTurnoverRenewalAsync` — simula confirmação
   depois do `EndDate` (retentativa).
4. `CancelledRenewal_WithoutRenewalPaidForCycle_StillExpiresNormally` — aluno cancela a renovação
   antes de qualquer nova cobrança → expira normalmente, sem passar pelo caminho de virada.

Mais `ListDueForTurnoverAsync_ReturnsOnlyRenewalPaidPackagesPastEndDate` e
`ListExpiredIdsAsync_ExcludesPackagesWithRenewalPaidForCycle` (garantem que os dois caminhos
— expirar vs. virar — nunca se sobrepõem pro mesmo pacote).

## 6. Pontos de atenção

- Sem ambiente de sandbox da Asaas nesta sessão — a parte de cobrança em si (não alterada aqui)
  segue com a mesma ressalva já registrada nos docs anteriores.
- `TurnoverAsync`/`ApplyRenewalSuccessAsync`'s ramo "depois do `EndDate`" ainda não tem teste
  cobrindo via `RecurringRenewalService` diretamente (só via `StudentPackageLifecycleService`
  chamado direto, simulando o que o serviço faria) — os mocks de `RecurringRenewalService` já são
  numerosos (Asaas, cartão, e-mail, etc.); um teste de integração mais completo fica pra uma
  revisão futura se a cliente pedir.
