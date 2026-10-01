using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sinchrony.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCounterChannelOncePerStudentBirthdayTeacherRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_purchases_PackageId",
                table: "purchases");

            migrationBuilder.AddColumn<DateOnly>(
                name: "BirthDate",
                table: "users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "users",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Channel",
                table: "purchases",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "app");

            migrationBuilder.AddColumn<bool>(
                name: "OncePerStudent",
                table: "packages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "class_rates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Value = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_class_rates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_class_rates_class_types_ClassTypeId",
                        column: x => x.ClassTypeId,
                        principalTable: "class_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "teacher_bonus_rates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ValuePerStudent = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teacher_bonus_rates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_purchases_PackageId_UserId_Status",
                table: "purchases",
                columns: new[] { "PackageId", "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_class_rates_ClassTypeId_EffectiveFrom",
                table: "class_rates",
                columns: new[] { "ClassTypeId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_teacher_bonus_rates_EffectiveFrom",
                table: "teacher_bonus_rates",
                column: "EffectiveFrom");

            // Backfill do canal: dinheiro e cortesia só existiam no balcão (concessão manual do ERP);
            // todo o resto veio do App (a coluna já nasce com default 'app').
            migrationBuilder.Sql(@"
                UPDATE purchases SET ""Channel"" = 'balcao'
                WHERE ""PaymentMethod"" IN ('cash', 'courtesy');
            ");

            // Permissões novas das observações do aluno (student_notes). Mesmo padrão do seed de
            // AddPermissionsSystem: entram no catálogo e todo admin existente recebe as duas
            // automaticamente (GrantedByUserId = NULL = concessão automática de migração).
            migrationBuilder.Sql(@"
                INSERT INTO permissions (""Id"", ""Resource"", ""Action"")
                SELECT gen_random_uuid(), 'student_notes', a.action
                FROM (VALUES ('view'), ('edit')) AS a(action)
                WHERE NOT EXISTS (
                    SELECT 1 FROM permissions p
                    WHERE p.""Resource"" = 'student_notes' AND p.""Action"" = a.action);
            ");

            migrationBuilder.Sql(@"
                INSERT INTO user_permissions (""Id"", ""UserId"", ""PermissionId"", ""GrantedAt"", ""GrantedByUserId"")
                SELECT gen_random_uuid(), u.""Id"", p.""Id"", now(), NULL
                FROM users u
                CROSS JOIN permissions p
                WHERE u.""Role"" = 'admin' AND p.""Resource"" = 'student_notes'
                  AND NOT EXISTS (
                    SELECT 1 FROM user_permissions up
                    WHERE up.""UserId"" = u.""Id"" AND up.""PermissionId"" = p.""Id"");
            ");

            // Seed dos valores do professor, com vigência antiga (01/01/2026) para cobrir os
            // relatórios retroativos: aula padrão R$ 65,00 e bônus R$ 3,00 por aluno presente.
            migrationBuilder.Sql(@"
                INSERT INTO class_rates (""Id"", ""ClassTypeId"", ""Value"", ""EffectiveFrom"", ""CreatedAt"", ""CreatedById"")
                VALUES (gen_random_uuid(), NULL, 65.00, DATE '2026-01-01', now(), NULL);

                INSERT INTO teacher_bonus_rates (""Id"", ""ValuePerStudent"", ""EffectiveFrom"", ""CreatedAt"", ""CreatedById"")
                VALUES (gen_random_uuid(), 3.00, DATE '2026-01-01', now(), NULL);
            ");

            // Jiu-jítsu autista: R$ 75,00. Resolvido pelo Id do ClassType cadastrado (a linha de valor
            // guarda o ClassTypeId, não o nome — o nome é usado só aqui, uma vez, para achar o Id).
            // Se o ambiente não tiver essa modalidade, nada é inserido: cadastre depois em
            // POST /api/class-rates.
            migrationBuilder.Sql(@"
                INSERT INTO class_rates (""Id"", ""ClassTypeId"", ""Value"", ""EffectiveFrom"", ""CreatedAt"", ""CreatedById"")
                SELECT gen_random_uuid(), ct.""Id"", 75.00, DATE '2026-01-01', now(), NULL
                FROM class_types ct
                WHERE ct.""Name"" ILIKE '%jiu%' AND ct.""Name"" ILIKE '%autista%';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // O seed de permissões student_notes sai junto: user_permissions tem FK com cascade.
            migrationBuilder.Sql(@"DELETE FROM permissions WHERE ""Resource"" = 'student_notes';");

            migrationBuilder.DropTable(
                name: "class_rates");

            migrationBuilder.DropTable(
                name: "teacher_bonus_rates");

            migrationBuilder.DropIndex(
                name: "IX_purchases_PackageId_UserId_Status",
                table: "purchases");

            migrationBuilder.DropColumn(
                name: "BirthDate",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "purchases");

            migrationBuilder.DropColumn(
                name: "OncePerStudent",
                table: "packages");

            migrationBuilder.CreateIndex(
                name: "IX_purchases_PackageId",
                table: "purchases",
                column: "PackageId");
        }
    }
}
