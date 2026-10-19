using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sinchrony.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTermsAcceptanceAndTermsV4Rules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EarlyCancellationFeeAmount",
                table: "student_packages",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EarlyCancellationFeeWaiveReason",
                table: "student_packages",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EarlyCancellationFeeWaived",
                table: "student_packages",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EarlyCancellationFeePercent",
                table: "packages",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FreeNoShowsPerPackage",
                table: "packages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumCycles",
                table: "packages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultFreeNoShowsPerPackage",
                table: "package_types",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "terms_acceptances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TermsVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClientAcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Channel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AppVersion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_terms_acceptances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_terms_acceptances_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_terms_acceptances_UserId_AcceptedAt",
                table: "terms_acceptances",
                columns: new[] { "UserId", "AcceptedAt" });

            // Quem já tem aceite gravado entra no histórico como "legacy": sem IP, aparelho nem
            // versão do App — dado antigo e de menor valor como prova, nada inventado.
            migrationBuilder.Sql("""
                INSERT INTO terms_acceptances
                    ("Id", "UserId", "TermsVersion", "AcceptedAt", "ClientAcceptedAt", "Channel", "IpAddress", "UserAgent", "AppVersion")
                SELECT gen_random_uuid(), u."Id", COALESCE(u."TermsVersion", 'unknown'), u."TermsAcceptedAt", NULL, 'legacy', NULL, NULL, NULL
                FROM users u
                WHERE u."TermsAcceptedAt" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "terms_acceptances");

            migrationBuilder.DropColumn(
                name: "EarlyCancellationFeeAmount",
                table: "student_packages");

            migrationBuilder.DropColumn(
                name: "EarlyCancellationFeeWaiveReason",
                table: "student_packages");

            migrationBuilder.DropColumn(
                name: "EarlyCancellationFeeWaived",
                table: "student_packages");

            migrationBuilder.DropColumn(
                name: "EarlyCancellationFeePercent",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "FreeNoShowsPerPackage",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "MinimumCycles",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "DefaultFreeNoShowsPerPackage",
                table: "package_types");
        }
    }
}
