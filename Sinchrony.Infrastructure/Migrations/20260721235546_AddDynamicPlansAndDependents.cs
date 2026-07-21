using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sinchrony.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDynamicPlansAndDependents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowWaitlist",
                table: "packages",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BookingWindowDays",
                table: "packages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CancellationDeadlineHours",
                table: "packages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreditsPerMember",
                table: "packages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EarlyAccessHours",
                table: "packages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxBookingsPerDay",
                table: "packages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxBookingsPerMonth",
                table: "packages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxBookingsPerWeek",
                table: "packages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxDependents",
                table: "packages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxFutureBookings",
                table: "packages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxNoShowsBeforeBlock",
                table: "packages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NoShowBlockWindowDays",
                table: "packages",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<bool>(
                name: "NoShowCreditPenalty",
                table: "packages",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PackageTypeId",
                table: "packages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PurchaseStrategy",
                table: "packages",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "block");

            migrationBuilder.AddColumn<bool>(
                name: "ReschedulingAllowed",
                table: "packages",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReschedulingDeadlineHours",
                table: "packages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WaitlistPriority",
                table: "packages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DependentId",
                table: "bookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "benefits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Icon = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_benefits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "dependents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponsibleStudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CanBook = table.Column<bool>(type: "boolean", nullable: false),
                    CanCancel = table.Column<bool>(type: "boolean", nullable: false),
                    CanViewHistory = table.Column<bool>(type: "boolean", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dependents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dependents_users_ResponsibleStudentId",
                        column: x => x.ResponsibleStudentId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "package_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    IsFamily = table.Column<bool>(type: "boolean", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: true),
                    DefaultMaxFutureBookings = table.Column<int>(type: "integer", nullable: true),
                    DefaultMaxBookingsPerDay = table.Column<int>(type: "integer", nullable: true),
                    DefaultMaxBookingsPerWeek = table.Column<int>(type: "integer", nullable: true),
                    DefaultMaxBookingsPerMonth = table.Column<int>(type: "integer", nullable: true),
                    DefaultCancellationDeadlineHours = table.Column<int>(type: "integer", nullable: true),
                    DefaultBookingWindowDays = table.Column<int>(type: "integer", nullable: true),
                    DefaultEarlyAccessHours = table.Column<int>(type: "integer", nullable: true),
                    DefaultAllowWaitlist = table.Column<bool>(type: "boolean", nullable: true),
                    DefaultReschedulingAllowed = table.Column<bool>(type: "boolean", nullable: true),
                    DefaultReschedulingDeadlineHours = table.Column<int>(type: "integer", nullable: true),
                    DefaultNoShowCreditPenalty = table.Column<bool>(type: "boolean", nullable: true),
                    DefaultMaxNoShowsBeforeBlock = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_package_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "student_packages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PurchasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_packages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_student_packages_packages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_packages_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "package_benefits",
                columns: table => new
                {
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    BenefitId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_package_benefits", x => new { x.PackageId, x.BenefitId });
                    table.ForeignKey(
                        name: "FK_package_benefits_benefits_BenefitId",
                        column: x => x.BenefitId,
                        principalTable: "benefits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_package_benefits_packages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dependent_package_allocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentPackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    DependentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreditsRemaining = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dependent_package_allocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dependent_package_allocations_dependents_DependentId",
                        column: x => x.DependentId,
                        principalTable: "dependents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_dependent_package_allocations_student_packages_StudentPacka~",
                        column: x => x.StudentPackageId,
                        principalTable: "student_packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_packages_PackageTypeId",
                table: "packages",
                column: "PackageTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_DependentId",
                table: "bookings",
                column: "DependentId");

            migrationBuilder.CreateIndex(
                name: "IX_dependent_package_allocations_DependentId",
                table: "dependent_package_allocations",
                column: "DependentId");

            migrationBuilder.CreateIndex(
                name: "IX_dependent_package_allocations_StudentPackageId",
                table: "dependent_package_allocations",
                column: "StudentPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_dependents_ResponsibleStudentId",
                table: "dependents",
                column: "ResponsibleStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_package_benefits_BenefitId",
                table: "package_benefits",
                column: "BenefitId");

            migrationBuilder.CreateIndex(
                name: "IX_student_packages_PackageId",
                table: "student_packages",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_student_packages_StudentId_Status",
                table: "student_packages",
                columns: new[] { "StudentId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_bookings_dependents_DependentId",
                table: "bookings",
                column: "DependentId",
                principalTable: "dependents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_packages_package_types_PackageTypeId",
                table: "packages",
                column: "PackageTypeId",
                principalTable: "package_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bookings_dependents_DependentId",
                table: "bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_packages_package_types_PackageTypeId",
                table: "packages");

            migrationBuilder.DropTable(
                name: "dependent_package_allocations");

            migrationBuilder.DropTable(
                name: "package_benefits");

            migrationBuilder.DropTable(
                name: "package_types");

            migrationBuilder.DropTable(
                name: "dependents");

            migrationBuilder.DropTable(
                name: "student_packages");

            migrationBuilder.DropTable(
                name: "benefits");

            migrationBuilder.DropIndex(
                name: "IX_packages_PackageTypeId",
                table: "packages");

            migrationBuilder.DropIndex(
                name: "IX_bookings_DependentId",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "AllowWaitlist",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "BookingWindowDays",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "CancellationDeadlineHours",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "CreditsPerMember",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "EarlyAccessHours",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "MaxBookingsPerDay",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "MaxBookingsPerMonth",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "MaxBookingsPerWeek",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "MaxDependents",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "MaxFutureBookings",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "MaxNoShowsBeforeBlock",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "NoShowBlockWindowDays",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "NoShowCreditPenalty",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "PackageTypeId",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "PurchaseStrategy",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "ReschedulingAllowed",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "ReschedulingDeadlineHours",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "WaitlistPriority",
                table: "packages");

            migrationBuilder.DropColumn(
                name: "DependentId",
                table: "bookings");
        }
    }
}
