using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eryph.StateDb.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddCatletProvisioningStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSeenProvisioningStatus",
                table: "Catlets",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "ProvisioningStatus",
                table: "Catlets",
                type: "longtext",
                nullable: false,
                // Back-fill existing catlets with a parseable enum name. The model
                // keeps no default; this only seeds pre-existing rows on upgrade.
                defaultValue: "Unknown")
                .Annotation("MySql:CharSet", "utf8mb4");
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
