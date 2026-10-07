using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedFlow.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientInsuranceDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InsuranceGroupNumber",
                table: "Patients",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InsurancePayerId",
                table: "Patients",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "InsuranceSubscriberDateOfBirth",
                table: "Patients",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InsuranceSubscriberName",
                table: "Patients",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InsuranceSubscriberRelationship",
                table: "Patients",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InsuranceGroupNumber",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "InsurancePayerId",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "InsuranceSubscriberDateOfBirth",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "InsuranceSubscriberName",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "InsuranceSubscriberRelationship",
                table: "Patients");
        }
    }
}
