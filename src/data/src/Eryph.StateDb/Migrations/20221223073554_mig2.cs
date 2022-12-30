using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eryph.StateDb.Migrations
{
    public partial class mig2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CpuCount",
                table: "VCatlets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Features",
                table: "VCatlets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MaximumMemory",
                table: "VCatlets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "MinimumMemory",
                table: "VCatlets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "SecureBootTemplate",
                table: "VCatlets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "StartupMemory",
                table: "VCatlets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CpuCount",
                table: "VCatlets");

            migrationBuilder.DropColumn(
                name: "Features",
                table: "VCatlets");

            migrationBuilder.DropColumn(
                name: "MaximumMemory",
                table: "VCatlets");

            migrationBuilder.DropColumn(
                name: "MinimumMemory",
                table: "VCatlets");

            migrationBuilder.DropColumn(
                name: "SecureBootTemplate",
                table: "VCatlets");

            migrationBuilder.DropColumn(
                name: "StartupMemory",
                table: "VCatlets");
        }
    }
}
