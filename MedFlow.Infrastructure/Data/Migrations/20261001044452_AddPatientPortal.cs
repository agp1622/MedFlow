using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedFlow.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PortalUserId",
                table: "Patients",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SharedWithPatient",
                table: "PatientAttachments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SharedWithPatient",
                table: "MedicalNotes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PortalAccessLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    ResourceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ResourceId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortalAccessLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PortalInvitations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortalInvitations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_PortalUserId",
                table: "Patients",
                column: "PortalUserId",
                unique: true,
                filter: "[PortalUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PortalAccessLogs_PatientId_OccurredAt",
                table: "PortalAccessLogs",
                columns: new[] { "PatientId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PortalInvitations_PatientId",
                table: "PortalInvitations",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PortalInvitations_TokenHash",
                table: "PortalInvitations",
                column: "TokenHash");

            // Roles: create Doctor/Patient and grant Doctor to every existing doctor account so that
            // role-gated endpoints keep working for current users (fail-closed for everyone else).
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM AspNetRoles WHERE NormalizedName = 'DOCTOR')
    INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
    VALUES (CONVERT(nvarchar(450), NEWID()), 'Doctor', 'DOCTOR', CONVERT(nvarchar(36), NEWID()));
IF NOT EXISTS (SELECT 1 FROM AspNetRoles WHERE NormalizedName = 'PATIENT')
    INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
    VALUES (CONVERT(nvarchar(450), NEWID()), 'Patient', 'PATIENT', CONVERT(nvarchar(36), NEWID()));");

            migrationBuilder.Sql(@"
INSERT INTO AspNetUserRoles (UserId, RoleId)
SELECT DISTINCT d.UserId, r.Id
FROM Doctors d
CROSS JOIN AspNetRoles r
WHERE r.NormalizedName = 'DOCTOR'
  AND NOT EXISTS (SELECT 1 FROM AspNetUserRoles ur WHERE ur.UserId = d.UserId AND ur.RoleId = r.Id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM AspNetUserRoles WHERE RoleId IN (SELECT Id FROM AspNetRoles WHERE NormalizedName IN ('DOCTOR', 'PATIENT'));
DELETE FROM AspNetRoles WHERE NormalizedName IN ('DOCTOR', 'PATIENT');");

            migrationBuilder.DropTable(
                name: "PortalAccessLogs");

            migrationBuilder.DropTable(
                name: "PortalInvitations");

            migrationBuilder.DropIndex(
                name: "IX_Patients_PortalUserId",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "PortalUserId",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "SharedWithPatient",
                table: "PatientAttachments");

            migrationBuilder.DropColumn(
                name: "SharedWithPatient",
                table: "MedicalNotes");
        }
    }
}
