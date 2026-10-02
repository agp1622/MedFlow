using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedFlow.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSecureMessaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActorUserId",
                table: "PortalAccessLogs",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MessageId",
                table: "PatientAttachments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MessageThreads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    DoctorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    LastMessageAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageThreads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ThreadId = table.Column<int>(type: "int", nullable: false),
                    SenderRole = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SenderUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_MessageThreads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "MessageThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientAttachments_MessageId",
                table: "PatientAttachments",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ThreadId_SenderRole_ReadAt",
                table: "Messages",
                columns: new[] { "ThreadId", "SenderRole", "ReadAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ThreadId_SentAt",
                table: "Messages",
                columns: new[] { "ThreadId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageThreads_DoctorId_LastMessageAt",
                table: "MessageThreads",
                columns: new[] { "DoctorId", "LastMessageAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageThreads_PatientId",
                table: "MessageThreads",
                column: "PatientId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "MessageThreads");

            migrationBuilder.DropIndex(
                name: "IX_PatientAttachments_MessageId",
                table: "PatientAttachments");

            migrationBuilder.DropColumn(
                name: "ActorUserId",
                table: "PortalAccessLogs");

            migrationBuilder.DropColumn(
                name: "MessageId",
                table: "PatientAttachments");
        }
    }
}
