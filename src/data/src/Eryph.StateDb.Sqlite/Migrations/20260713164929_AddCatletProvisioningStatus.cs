using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eryph.StateDb.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddCatletProvisioningStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeenProvisioningStatus",
                table: "Catlets",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "ProvisioningStatus",
                table: "Catlets",
                type: "TEXT",
                nullable: false,
                // Back-fill existing catlets with a parseable enum name. The model
                // keeps no default; this only seeds pre-existing rows on upgrade.
                defaultValue: "Unknown");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastSeenProvisioningStatus",
                table: "Catlets");

            migrationBuilder.DropColumn(
                name: "ProvisioningStatus",
                table: "Catlets");
        }
    }
}
