using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HyperVPlus.StateDb.MySql.Migrations
{
    public partial class test2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MachineId",
                table: "Agents",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Agents_MachineId",
                table: "Agents",
                column: "MachineId");

            migrationBuilder.AddForeignKey(
                name: "FK_Agents_Machines_MachineId",
                table: "Agents",
                column: "MachineId",
                principalTable: "Machines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Agents_Machines_MachineId",
                table: "Agents");

            migrationBuilder.DropIndex(
                name: "IX_Agents_MachineId",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "MachineId",
                table: "Agents");
        }
    }
}
