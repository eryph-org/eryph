using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eryph.StateDb.Migrations
{
    public partial class StorageIds : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Frozen",
                table: "VDisks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DataStore",
                table: "VCatlets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Environment",
                table: "VCatlets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Frozen",
                table: "VCatlets",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StorageIdentifier",
                table: "VCatlets",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Frozen",
                table: "VDisks");

            migrationBuilder.DropColumn(
                name: "DataStore",
                table: "VCatlets");

            migrationBuilder.DropColumn(
                name: "Environment",
                table: "VCatlets");

            migrationBuilder.DropColumn(
                name: "Frozen",
                table: "VCatlets");

            migrationBuilder.DropColumn(
                name: "StorageIdentifier",
                table: "VCatlets");
        }
    }
}
