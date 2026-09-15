using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sinchrony.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionsSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Resource = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GrantedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_permissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_permissions_permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_permissions_users_GrantedByUserId",
                        column: x => x.GrantedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_user_permissions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_permissions_Resource_Action",
                table: "permissions",
                columns: new[] { "Resource", "Action" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_permissions_GrantedByUserId",
                table: "user_permissions",
                column: "GrantedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_permissions_PermissionId",
                table: "user_permissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_user_permissions_UserId_PermissionId",
                table: "user_permissions",
                columns: new[] { "UserId", "PermissionId" },
                unique: true);

            // Seed 1/2 — catálogo fixo (Resource × Action). Lista de resources conforme
            // DEMANDA_CONTROLE_ADMIN_PERMISSOES_BACKEND.md, Fase 0. gen_random_uuid() é
            // built-in a partir do Postgres 13 (produção roda postgres:16-alpine), sem
            // precisar da extensão pgcrypto.
            migrationBuilder.Sql(@"
                INSERT INTO permissions (""Id"", ""Resource"", ""Action"")
                SELECT gen_random_uuid(), r.resource, a.action
                FROM (VALUES
                    ('student'), ('teacher'), ('class'), ('package'), ('credit'),
                    ('audit_log'), ('bike'), ('coupon'), ('studio'), ('unit'),
                    ('class_type'), ('package_type'), ('benefit')
                ) AS r(resource)
                CROSS JOIN (VALUES ('create'), ('edit'), ('view'), ('delete')) AS a(action);
            ");

            // Seed 2/2 — todo usuário Role.admin existente recebe automaticamente todas as
            // permissões do catálogo. GrantedByUserId = NULL identifica concessão automática
            // de migração, não uma ação humana. É isso que garante ""acesso total por padrão""
            // pro admin sem nenhum caso especial no código de checagem, e garante que nenhum
            // admin perde acesso no dia do deploy.
            migrationBuilder.Sql(@"
                INSERT INTO user_permissions (""Id"", ""UserId"", ""PermissionId"", ""GrantedAt"", ""GrantedByUserId"")
                SELECT gen_random_uuid(), u.""Id"", p.""Id"", now(), NULL
                FROM users u
                CROSS JOIN permissions p
                WHERE u.""Role"" = 'admin';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_permissions");

            migrationBuilder.DropTable(
                name: "permissions");
        }
    }
}
