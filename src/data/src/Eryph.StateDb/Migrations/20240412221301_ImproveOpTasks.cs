using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eryph.StateDb.Migrations
{
    public partial class ImproveOpTasks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Progress",
                table: "OperationTasks");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastUpdated",
                table: "OperationTasks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastUpdated",
                table: "Operations",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateTable(
                name: "TaskProgress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TaskId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Progress = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskProgress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskProgress_OperationTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "OperationTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskProgress_TaskId",
                table: "TaskProgress",
                column: "TaskId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskProgress");

            migrationBuilder.DropColumn(
                name: "LastUpdated",
                table: "OperationTasks");

            migrationBuilder.DropColumn(
                name: "LastUpdated",
                table: "Operations");

            migrationBuilder.AddColumn<int>(
                name: "Progress",
                table: "OperationTasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
