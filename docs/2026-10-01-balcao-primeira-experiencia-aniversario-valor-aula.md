# Balcão, Primeira Experiência, dados do aluno e relatório de professores

Data: 01/10/2026 · Escopo: back-end (API) · Status: implementado localmente, ainda não commitado

Origem: `DEMANDA_BACKEND_DEMANDAS_PENDENTES_2026-09-30.md` (itens 1 a 6). Compila; 120 testes
passam (os 120 incluem os novos). **Migration não foi aplicada em Postgres real** (sem banco/Docker
disponível na máquina de desenvolvimento): o SQL foi gerado e conferido por `dotnet ef migrations script`.

## 1. Migration `20261001120851_AddCounterChannelOncePerStudentBirthdayTeacherRates`

- `purchases.Channel varchar(10) NOT NULL DEFAULT 'app'`; backfill `cash`/`courtesy` → `balcao`.
- `packages.OncePerStudent bool NOT NULL DEFAULT false`.
- `users.BirthDate date NULL`, `users.Notes varchar(1000) NULL` (sem backfill).
- Tabelas `class_rates` e `teacher_bonus_rates`, com seed vigente desde 01/01/2026:
  padrão R$ 65,00; bônus R$ 3,00; R$ 75,00 para o(s) `ClassType` cujo nome contém "jiu" e
  "autista" (o nome só é usado na migration para achar o `Id`; se o ambiente não tiver essa
  modalidade nada é inserido — cadastrar depois em `POST /api/class-rates`).
- Permissões `student_notes/view` e `student_notes/edit` no catálogo, concedidas a todo admin existente.
- Índice `purchases(PackageId, UserId, Status)` para a regra de compra única (substitui o índice só por `PackageId`).

## 2. Contrato

**Balcão (item 1)** — `Purchase.Channel` = `app` | `balcao`. `POST /api/students/{id}/packages`
aceita `paymentMethod` `pix`, `card`, `cash` ou `courtesy`; `amount` obrigatório exceto cortesia;
grava `Channel = balcao`, `Status = confirmed` (valores de `Status` inalterados). `GET /api/purchases`
devolve `channel`, aceita `channel=app|balcao` e os 4 valores em `paymentMethod`.

**Pacote inativo (item 2)** — `/payments/pix` e `/payments/card` recusam com `PACKAGE_INACTIVE`.

**Compra única (item 3)** — `Package.OncePerStudent` no CRUD (`oncePerStudent`) e nos `GET` de pacotes.
Erros: `PACKAGE_ALREADY_PURCHASED` (409) e `PAYMENT_ALREADY_IN_PROGRESS` (409). Detalhes:

- "Família" = o usuário, o responsável dele e os dependentes dele, nos dois modelos
  (`dependents` e `users.IsDependent`). Irmãos (dois dependentes do mesmo titular) não se bloqueiam entre si.
- Só `confirmed` conta; a concessão no balcão conta; sem exceção para admin.
- PIX pendente ainda válido: `PayWithPix`/`PurchasePackage(pix)` devolvem o **mesmo PIX**
  (`IAsaasService.GetPixQrCodeAsync`). Só é reaproveitado se for do próprio usuário; se for de outro
  membro da família, recusa (senão o titular pagaria créditos do dependente). Pelo cartão ou pelo
  balcão, PIX em aberto recusa (balcão: "Existe um pagamento pendente deste pacote pelo app.").
- Cartão em análise recusa. Carrinho com vários pacotes em que um cai numa regra: recusa a compra inteira.
- PIX vencido = a partir do começo do dia seguinte ao `dueDate` da cobrança (ela é pagável até o fim
  do dia de vencimento). Confere no Asaas: pago → confirma pelo fluxo normal e recusa a nova compra;
  não pago → `DELETE /payments/{id}`, purchase antiga `failed`, e a nova cobrança é gerada.
- Concorrência: `OncePerStudentGuard.RunLockedAsync` abre transação e pega `pg_advisory_xact_lock`
  por (titular da família + pacote). Erros de bloqueio da própria guarda fazem commit (o que a
  checagem gravou é legítimo); qualquer outro erro faz rollback.
- O fluxo de confirmação do webhook foi extraído para `IPaymentConfirmationService` (mesmo código,
  agora também usado quando se descobre por consulta ao Asaas que um PIX vencido foi pago).
- `AsaasService.GetPaymentAsync` agora devolve `PAYMENT_DELETED` em 404 (cobrança cancelada).

**Dados do aluno (item 4)** — `User.BirthDate`/`User.Notes`. ERP: `birthDate` em toda resposta;
`notes` só para admin ou `student_notes:view` (senão o campo é omitido); `notes` sem
`student_notes:edit` (e sem ser admin) → `403`; `birthDate` diferente do salvo por não-admin → `403`.
App: `PUT /profile` aceita `birthDate` só se ainda for nulo, senão `422 BIRTHDATE_ALREADY_SET`
(reenviar o mesmo valor é aceito); `birthDate` volta em login, `/auth/me` e `PUT /profile`; `notes`
nunca vai para o aluno. `notes: null` = não altera; `""` = limpa. Anonimização limpa os dois.
`GET /api/reports/birthdays?month=1..12` (admin e professor, escopo de unidade igual ao da lista de
alunos): `studentId, name, birthDate, day, month, phone, email, status`, ordenado por dia.

**Relatório de professores (item 5)** — `GET/POST /api/class-rates` e `/api/teacher-bonus-rates`
(admin; mudança de preço = registro novo com `effectiveFrom`). `GET /api/reports/teacher-classes`
(admin, escopo de unidade): filtros `from,to,teacherId,classTypeId,studioId,classStatus(lista),minAttended`;
uma linha por aula com `booked, attended, noShow, cancelledBookings, classValue, bonusPerStudent, bonus, subtotal`
e `totals{totalClasses,totalAttended,totalValue,totalBonus,total,byClassStatus}`. `attended` =
`AttendanceRecord.Status == attended`; `booked` = reservas que não são `cancelled` nem `waitlisted`.

**Ocupação (item 6)** — `classType` na linha; `from`/`to` além de `days`; `booked` conta tudo que não é
cancelado nem lista de espera (antes a reserva sumia ao lançar a presença).

## 3. Testes

`dotnet test`: 120 aprovados. Novos: `OncePerStudentGuardTests`, `TeacherRateResolverTests`,
`TeacherClassReportRepositoryTests`, `StudentProfileAndCounterPurchaseTests`.
`CreateBookingCommandTests.cs` continua não compilando por motivo pré-existente e não relacionado
(assinatura desatualizada de `CreateBookingCommand`) — foi excluído temporariamente só para rodar a suíte.

## 4. Ajustes pós-revisão (`DEMANDA_AJUSTES_POS_REVISAO_2026-10-01_BACKEND_v2.md`)

1. `GET /purchases` (App) devolve `transactionId` e `channel`.
2. Observações decididas só por `student_notes:view/edit`, sem atalho por role admin.
3. Dependente (qualquer dos dois modelos) não compra pacote `OncePerStudent` nos 4 caminhos, inclusive balcão:
   `409 PACKAGE_NOT_AVAILABLE_FOR_DEPENDENT`, checado antes das compras da família.
4. `PixPaymentResponseDto.Reused` (`true` quando devolve o PIX já existente).
5. `notes` igual ao salvo (normalizado) não conta como edição.
6. `clearBirthDate: true` no `PUT /api/students/{id}` (só admin; junto com `birthDate` → 422 `BIRTHDATE_CONFLICT`).

Sem migration. A decisão de irmãos (item "fora desta demanda") ficou como estava.
