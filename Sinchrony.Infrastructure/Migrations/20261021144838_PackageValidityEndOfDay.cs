using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sinchrony.Infrastructure.Migrations
{
    // DEMANDA_VALIDADE_FIM_DO_DIA_E_FILA_AO_ZERAR_CREDITOS_BACKEND.md, Regra 1 (21/10/2026): a
    // validade passa a ir até 23:59:59 (Brasília) do último dia — ver PackageValidity. Leva os
    // pacotes em andamento (active e queued) pro fim do MESMO dia em Brasília: nada vence antes do
    // horário de hoje, só depois. Idempotente (quem já está em 23:59:59 BRT não muda).
    //
    // A Regra 2 (fila entra quando os créditos acabam) não precisa de migração: o
    // PackageExpirationService promove, já no primeiro minuto depois do deploy, quem estiver com
    // saldo 0 e pacote elegível na fila — pelo mesmo PromoteAsync, com extrato e auditoria.
    /// <inheritdoc />
    public partial class PackageValidityEndOfDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE student_packages
                SET "EndDate" = ((("EndDate" AT TIME ZONE 'America/Sao_Paulo')::date + TIME '23:59:59')
                                 AT TIME ZONE 'America/Sao_Paulo')
                WHERE "Status" IN ('active', 'queued')
                  AND ("EndDate" AT TIME ZONE 'America/Sao_Paulo')::time <> TIME '23:59:59';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sem volta: o horário original da compra não fica guardado no EndDate.
        }
    }
}
