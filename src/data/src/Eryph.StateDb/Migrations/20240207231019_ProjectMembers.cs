using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eryph.StateDb.Migrations
{
    public partial class ProjectMembers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectProjectRoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProjectRoles",
                table: "ProjectRoles");

            migrationBuilder.DropColumn(
                name: "AccessRight",
                table: "ProjectRoles");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "ProjectRoles",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "IdentityId",
                table: "ProjectRoles",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "OperationTasks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceId",
                table: "OperationTasks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceProjectName",
                table: "OperationTasks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReferenceType",
                table: "OperationTasks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ProjectRoles_ProjectId_IdentityId_RoleId",
                table: "ProjectRoles",
                columns: new[] { "ProjectId", "IdentityId", "RoleId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProjectRoles",
                table: "ProjectRoles",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_OperationTasks_ProjectId",
                table: "OperationTasks",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_OperationTasks_Projects_ProjectId",
                table: "OperationTasks",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectRoles_Projects_ProjectId",
                table: "ProjectRoles",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OperationTasks_Projects_ProjectId",
                table: "OperationTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectRoles_Projects_ProjectId",
                table: "ProjectRoles");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ProjectRoles_ProjectId_IdentityId_RoleId",
                table: "ProjectRoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProjectRoles",
                table: "ProjectRoles");

            migrationBuilder.DropIndex(
                name: "IX_OperationTasks_ProjectId",
                table: "OperationTasks");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "ProjectRoles");

            migrationBuilder.DropColumn(
                name: "IdentityId",
                table: "ProjectRoles");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "OperationTasks");

            migrationBuilder.DropColumn(
                name: "ReferenceId",
                table: "OperationTasks");

            migrationBuilder.DropColumn(
                name: "ReferenceProjectName",
                table: "OperationTasks");

            migrationBuilder.DropColumn(
                name: "ReferenceType",
                table: "OperationTasks");

            migrationBuilder.AddColumn<int>(
                name: "AccessRight",
                table: "ProjectRoles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProjectRoles",
                table: "ProjectRoles",
                columns: new[] { "RoleId", "ProjectId" });

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
    }
}
