using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HyperVPlus.StateDb.MySql.Migrations
{
    public partial class test : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MachineGuid",
                table: "Operations",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Machines",
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "Timestamp",
                table: "Logs",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateTable(
                name: "Agents",
                columns: table => new
                {
                    Name = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agents", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "Ipv4Addresses",
                columns: table => new
                {
                    MachineId = table.Column<Guid>(nullable: false),
                    Address = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ipv4Addresses", x => new { x.MachineId, x.Address });
                    table.ForeignKey(
                        name: "FK_Ipv4Addresses_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ipv6Addresses",
                columns: table => new
                {
                    MachineId = table.Column<Guid>(nullable: false),
                    Address = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ipv6Addresses", x => new { x.MachineId, x.Address });
                    table.ForeignKey(
                        name: "FK_Ipv6Addresses_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Machines_Name",
                table: "Machines",
                column: "Name");

            migrationBuilder.AddForeignKey(
                name: "FK_Machines_Agents_Name",
                table: "Machines",
                column: "Name",
                principalTable: "Agents",
                principalColumn: "Name",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Machines_Agents_Name",
                table: "Machines");

            migrationBuilder.DropTable(
                name: "Agents");

            migrationBuilder.DropTable(
                name: "Ipv4Addresses");

            migrationBuilder.DropTable(
                name: "Ipv6Addresses");

            migrationBuilder.DropIndex(
                name: "IX_Machines_Name",
                table: "Machines");

            migrationBuilder.DropColumn(
                name: "MachineGuid",
                table: "Operations");

            migrationBuilder.DropColumn(
                name: "Timestamp",
                table: "Logs");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Machines",
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);
        }
    }
}
