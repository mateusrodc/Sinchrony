using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sinchrony.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionStatusAndAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "student_packages",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoRenew",
                table: "student_packages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "RenewalCardId",
                table: "student_packages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RenewalAttempts",
                table: "student_packages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextRenewalAttemptAt",
                table: "student_packages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPaidAt",
                table: "student_packages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LastPaidAmount",
                table: "student_packages",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProblemSince",
                table: "student_packages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastFailureReason",
                table: "student_packages",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedAt",
                table: "student_packages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_student_packages_cards_RenewalCardId",
                table: "student_packages",
                column: "RenewalCardId",
                principalTable: "cards",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.CreateIndex(
                name: "IX_student_packages_RenewalCardId",
                table: "student_packages",
                column: "RenewalCardId");

            migrationBuilder.AddColumn<Guid>(
                name: "StudentPackageId",
                table: "purchases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "purchases",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "purchase");

            migrationBuilder.CreateIndex(
                name: "IX_purchases_StudentPackageId",
                table: "purchases",
                column: "StudentPackageId");

            migrationBuilder.AddForeignKey(
                name: "FK_purchases_student_packages_StudentPackageId",
                table: "purchases",
                column: "StudentPackageId",
                principalTable: "student_packages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateTable(
                name: "admin_alerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentPackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_alerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_admin_alerts_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_admin_alerts_student_packages_StudentPackageId",
                        column: x => x.StudentPackageId,
                        principalTable: "student_packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_admin_alerts_StudentId",
                table: "admin_alerts",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_alerts_StudentPackageId",
                table: "admin_alerts",
                column: "StudentPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_alerts_TransactionId",
                table: "admin_alerts",
                column: "TransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_alerts_ReadAt",
                table: "admin_alerts",
                column: "ReadAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_alerts");

            migrationBuilder.DropForeignKey(
                name: "FK_purchases_student_packages_StudentPackageId",
                table: "purchases");

            migrationBuilder.DropIndex(
                name: "IX_purchases_StudentPackageId",
                table: "purchases");

            migrationBuilder.DropColumn(
                name: "StudentPackageId",
                table: "purchases");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "purchases");

            migrationBuilder.DropForeignKey(
                name: "FK_student_packages_cards_RenewalCardId",
                table: "student_packages");

            migrationBuilder.DropIndex(
                name: "IX_student_packages_RenewalCardId",
                table: "student_packages");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "student_packages");

            migrationBuilder.DropColumn(
                name: "AutoRenew",
                table: "student_packages");

            migrationBuilder.DropColumn(
                name: "RenewalCardId",
                table: "student_packages");

            migrationBuilder.DropColumn(
                name: "RenewalAttempts",
                table: "student_packages");

            migrationBuilder.DropColumn(
                name: "NextRenewalAttemptAt",
                table: "student_packages");

            migrationBuilder.DropColumn(
                name: "LastPaidAt",
                table: "student_packages");

            migrationBuilder.DropColumn(
                name: "LastPaidAmount",
                table: "student_packages");

            migrationBuilder.DropColumn(
                name: "ProblemSince",
                table: "student_packages");

            migrationBuilder.DropColumn(
                name: "LastFailureReason",
                table: "student_packages");

            migrationBuilder.DropColumn(
                name: "LastSyncedAt",
                table: "student_packages");
        }
    }
}
