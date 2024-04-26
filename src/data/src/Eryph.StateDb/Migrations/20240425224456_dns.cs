using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eryph.StateDb.Migrations
{
    /// <inheritdoc />
    public partial class dns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DnsDomain",
                table: "Subnet",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "LastUpdated",
                table: "OperationTasks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressName",
                table: "NetworkPorts",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DnsDomain",
                table: "Subnet");

            migrationBuilder.DropColumn(
                name: "AddressName",
                table: "NetworkPorts");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "LastUpdated",
                table: "OperationTasks",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT");
        }
    }
}
