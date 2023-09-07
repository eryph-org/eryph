using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eryph.StateDb.Migrations
{
    public partial class catlets2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Tenants_OwnerId",
                table: "Projects");

            migrationBuilder.DropTable(
                name: "ProjectRolesSociety");

            migrationBuilder.RenameColumn(
                name: "OwnerId",
                table: "Projects",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_Projects_OwnerId",
                table: "Projects",
                newName: "IX_Projects_TenantId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Tenants_TenantId",
                table: "Projects",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Tenants_TenantId",
                table: "Projects");

            migrationBuilder.DropTable(
                name: "ProjectProjectRoles");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "Projects",
                newName: "OwnerId");

            migrationBuilder.RenameIndex(
                name: "IX_Projects_TenantId",
                table: "Projects",
                newName: "IX_Projects_OwnerId");

            migrationBuilder.CreateTable(
                name: "ProjectRolesSociety",
                columns: table => new
                {
                    ProjectsId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RolesRoleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RolesProjectId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectRolesSociety", x => new { x.ProjectsId, x.RolesRoleId, x.RolesProjectId });
                    table.ForeignKey(
                        name: "FK_ProjectRolesSociety_ProjectRoles_RolesRoleId_RolesProjectId",
                        columns: x => new { x.RolesRoleId, x.RolesProjectId },
                        principalTable: "ProjectRoles",
                        principalColumns: new[] { "RoleId", "ProjectId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectRolesSociety_Projects_ProjectsId",
                        column: x => x.ProjectsId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRolesSociety_RolesRoleId_RolesProjectId",
                table: "ProjectRolesSociety",
                columns: new[] { "RolesRoleId", "RolesProjectId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Tenants_OwnerId",
                table: "Projects",
                column: "OwnerId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
