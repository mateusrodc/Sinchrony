using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sinchrony.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GrantFullPermissionsToAdminsWithout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fase 5: as rotas de escrita passam a conferir permissão por módulo. Admin sem nenhuma
            // linha de permissão (criado pelo ERP/App depois do seed de AddPermissionsSystem) perderia
            // tudo no deploy — recebe o catálogo inteiro, como concessão automática (GrantedByUserId = NULL).
            // Admin que já tem alguma permissão gravada fica como está.
            migrationBuilder.Sql(@"
                INSERT INTO user_permissions (""Id"", ""UserId"", ""PermissionId"", ""GrantedAt"", ""GrantedByUserId"")
                SELECT gen_random_uuid(), u.""Id"", p.""Id"", now(), NULL
                FROM users u
                CROSS JOIN permissions p
                WHERE u.""Role"" = 'admin'
                  AND NOT EXISTS (SELECT 1 FROM user_permissions up WHERE up.""UserId"" = u.""Id"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sem volta: não dá para distinguir estas concessões das automáticas anteriores.
        }
    }
}
