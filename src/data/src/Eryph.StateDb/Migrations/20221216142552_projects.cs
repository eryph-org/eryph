using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eryph.StateDb.Migrations
{
    public partial class projects : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Operations_OperationId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_OperationId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "OperationId",
                table: "Projects");

            migrationBuilder.CreateTable(
                name: "OperationProject",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationProject", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationProject_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "Operations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OperationProject_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationProject_OperationId",
                table: "OperationProject",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationProject_ProjectId",
                table: "OperationProject",
                column: "ProjectId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperationProject");

            migrationBuilder.AddColumn<Guid>(
                name: "OperationId",
                table: "Projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OperationId",
                table: "Projects",
                column: "OperationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Operations_OperationId",
                table: "Projects",
                column: "OperationId",
                principalTable: "Operations",
                principalColumn: "Id");
        }
    }
}
