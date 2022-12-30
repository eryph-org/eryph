using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eryph.StateDb.Migrations
{
    public partial class peng2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectRoles",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccessRight = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectRoles", x => new { x.RoleId, x.ProjectId });
                });

            migrationBuilder.CreateTable(
                name: "ProjectProjectRoles",
                columns: table => new
                {
                    ProjectsId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RolesRoleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RolesProjectId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectProjectRoles", x => new { x.ProjectsId, x.RolesRoleId, x.RolesProjectId });
                    table.ForeignKey(
                        name: "FK_ProjectProjectRoles_ProjectRoles_RolesRoleId_RolesProjectId",
                        columns: x => new { x.RolesRoleId, x.RolesProjectId },
                        principalTable: "ProjectRoles",
                        principalColumns: new[] { "RoleId", "ProjectId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectProjectRoles_Projects_ProjectsId",
                        column: x => x.ProjectsId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectProjectRoles_RolesRoleId_RolesProjectId",
                table: "ProjectProjectRoles",
                columns: new[] { "RolesRoleId", "RolesProjectId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectProjectRoles");

            migrationBuilder.DropTable(
                name: "ProjectRoles");
        }
    }
}
