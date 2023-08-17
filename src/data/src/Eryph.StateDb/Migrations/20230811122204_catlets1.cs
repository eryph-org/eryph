using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eryph.StateDb.Migrations
{
    public partial class catlets1 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Tenants_TenantId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_VDisks_Resources_Id",
                table: "VDisks");

            migrationBuilder.DropForeignKey(
                name: "FK_VDisks_VDisks_ParentId",
                table: "VDisks");

            migrationBuilder.DropTable(
                name: "ProjectProjectRoles");

            migrationBuilder.DropTable(
                name: "VDrives");

            migrationBuilder.DropTable(
                name: "VirtualCatletNetworkAdapters");

            migrationBuilder.DropTable(
                name: "VCatlets");

            migrationBuilder.DropTable(
                name: "VCatletHosts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_VDisks",
                table: "VDisks");

            migrationBuilder.RenameTable(
                name: "VDisks",
                newName: "CatletDisks");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "Projects",
                newName: "OwnerId");

            migrationBuilder.RenameIndex(
                name: "IX_Projects_TenantId",
                table: "Projects",
                newName: "IX_Projects_OwnerId");

            migrationBuilder.RenameIndex(
                name: "IX_VDisks_ParentId",
                table: "CatletDisks",
                newName: "IX_CatletDisks_ParentId");

            migrationBuilder.AddColumn<int>(
                name: "CpuCount",
                table: "Catlets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DataStore",
                table: "Catlets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Environment",
                table: "Catlets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Features",
                table: "Catlets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Frozen",
                table: "Catlets",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "HostId",
                table: "Catlets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MaximumMemory",
                table: "Catlets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "MetadataId",
                table: "Catlets",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<long>(
                name: "MinimumMemory",
                table: "Catlets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Path",
                table: "Catlets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecureBootTemplate",
                table: "Catlets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "StartupMemory",
                table: "Catlets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "StorageIdentifier",
                table: "Catlets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VMId",
                table: "Catlets",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_CatletDisks",
                table: "CatletDisks",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "CatletDrives",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    CatletId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: true),
                    AttachedDiskId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatletDrives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatletDrives_CatletDisks_AttachedDiskId",
                        column: x => x.AttachedDiskId,
                        principalTable: "CatletDisks",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CatletDrives_Catlets_CatletId",
                        column: x => x.CatletId,
                        principalTable: "Catlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CatletFarms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HardwareId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatletFarms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatletFarms_Resources_Id",
                        column: x => x.Id,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CatletNetworkAdapters",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    CatletId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    SwitchName = table.Column<string>(type: "TEXT", nullable: true),
                    NetworkProviderName = table.Column<string>(type: "TEXT", nullable: true),
                    MacAddress = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatletNetworkAdapters", x => new { x.CatletId, x.Id });
                    table.ForeignKey(
                        name: "FK_CatletNetworkAdapters_Catlets_CatletId",
                        column: x => x.CatletId,
                        principalTable: "Catlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "IX_Catlets_HostId",
                table: "Catlets",
                column: "HostId");

            migrationBuilder.CreateIndex(
                name: "IX_CatletDrives_AttachedDiskId",
                table: "CatletDrives",
                column: "AttachedDiskId");

            migrationBuilder.CreateIndex(
                name: "IX_CatletDrives_CatletId",
                table: "CatletDrives",
                column: "CatletId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRolesSociety_RolesRoleId_RolesProjectId",
                table: "ProjectRolesSociety",
                columns: new[] { "RolesRoleId", "RolesProjectId" });

            migrationBuilder.AddForeignKey(
                name: "FK_CatletDisks_CatletDisks_ParentId",
                table: "CatletDisks",
                column: "ParentId",
                principalTable: "CatletDisks",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CatletDisks_Resources_Id",
                table: "CatletDisks",
                column: "Id",
                principalTable: "Resources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Catlets_CatletFarms_HostId",
                table: "Catlets",
                column: "HostId",
                principalTable: "CatletFarms",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Tenants_OwnerId",
                table: "Projects",
                column: "OwnerId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CatletDisks_CatletDisks_ParentId",
                table: "CatletDisks");

            migrationBuilder.DropForeignKey(
                name: "FK_CatletDisks_Resources_Id",
                table: "CatletDisks");

            migrationBuilder.DropForeignKey(
                name: "FK_Catlets_CatletFarms_HostId",
                table: "Catlets");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Tenants_OwnerId",
                table: "Projects");

            migrationBuilder.DropTable(
                name: "CatletDrives");

            migrationBuilder.DropTable(
                name: "CatletFarms");

            migrationBuilder.DropTable(
                name: "CatletNetworkAdapters");

            migrationBuilder.DropTable(
                name: "ProjectRolesSociety");

            migrationBuilder.DropIndex(
                name: "IX_Catlets_HostId",
                table: "Catlets");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CatletDisks",
                table: "CatletDisks");

            migrationBuilder.DropColumn(
                name: "CpuCount",
                table: "Catlets");

            migrationBuilder.DropColumn(
                name: "DataStore",
                table: "Catlets");

            migrationBuilder.DropColumn(
                name: "Environment",
                table: "Catlets");

            migrationBuilder.DropColumn(
                name: "Features",
                table: "Catlets");

            migrationBuilder.DropColumn(
                name: "Frozen",
                table: "Catlets");

            migrationBuilder.DropColumn(
                name: "HostId",
                table: "Catlets");

            migrationBuilder.DropColumn(
                name: "MaximumMemory",
                table: "Catlets");

            migrationBuilder.DropColumn(
                name: "MetadataId",
                table: "Catlets");

            migrationBuilder.DropColumn(
                name: "MinimumMemory",
                table: "Catlets");

            migrationBuilder.DropColumn(
                name: "Path",
                table: "Catlets");

            migrationBuilder.DropColumn(
                name: "SecureBootTemplate",
                table: "Catlets");

            migrationBuilder.DropColumn(
                name: "StartupMemory",
                table: "Catlets");

            migrationBuilder.DropColumn(
                name: "StorageIdentifier",
                table: "Catlets");

            migrationBuilder.DropColumn(
                name: "VMId",
                table: "Catlets");

            migrationBuilder.RenameTable(
                name: "CatletDisks",
                newName: "VDisks");

            migrationBuilder.RenameColumn(
                name: "OwnerId",
                table: "Projects",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_Projects_OwnerId",
                table: "Projects",
                newName: "IX_Projects_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_CatletDisks_ParentId",
                table: "VDisks",
                newName: "IX_VDisks_ParentId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_VDisks",
                table: "VDisks",
                column: "Id");

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

            migrationBuilder.CreateTable(
                name: "VCatletHosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HardwareId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VCatletHosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VCatletHosts_Catlets_Id",
                        column: x => x.Id,
                        principalTable: "Catlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VCatlets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HostId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CpuCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DataStore = table.Column<string>(type: "TEXT", nullable: true),
                    Environment = table.Column<string>(type: "TEXT", nullable: true),
                    Features = table.Column<string>(type: "TEXT", nullable: true),
                    Frozen = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaximumMemory = table.Column<long>(type: "INTEGER", nullable: false),
                    MetadataId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MinimumMemory = table.Column<long>(type: "INTEGER", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: true),
                    SecureBootTemplate = table.Column<string>(type: "TEXT", nullable: true),
                    StartupMemory = table.Column<long>(type: "INTEGER", nullable: false),
                    StorageIdentifier = table.Column<string>(type: "TEXT", nullable: true),
                    VMId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VCatlets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VCatlets_Catlets_Id",
                        column: x => x.Id,
                        principalTable: "Catlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VCatlets_VCatletHosts_HostId",
                        column: x => x.HostId,
                        principalTable: "VCatletHosts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VDrives",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    AttachedDiskId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MachineId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VDrives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VDrives_VCatlets_MachineId",
                        column: x => x.MachineId,
                        principalTable: "VCatlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VDrives_VDisks_AttachedDiskId",
                        column: x => x.AttachedDiskId,
                        principalTable: "VDisks",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VirtualCatletNetworkAdapters",
                columns: table => new
                {
                    MachineId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    MacAddress = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    NetworkProviderName = table.Column<string>(type: "TEXT", nullable: true),
                    SwitchName = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualCatletNetworkAdapters", x => new { x.MachineId, x.Id });
                    table.ForeignKey(
                        name: "FK_VirtualCatletNetworkAdapters_VCatlets_MachineId",
                        column: x => x.MachineId,
                        principalTable: "VCatlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectProjectRoles_RolesRoleId_RolesProjectId",
                table: "ProjectProjectRoles",
                columns: new[] { "RolesRoleId", "RolesProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_VCatlets_HostId",
                table: "VCatlets",
                column: "HostId");

            migrationBuilder.CreateIndex(
                name: "IX_VDrives_AttachedDiskId",
                table: "VDrives",
                column: "AttachedDiskId");

            migrationBuilder.CreateIndex(
                name: "IX_VDrives_MachineId",
                table: "VDrives",
                column: "MachineId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Tenants_TenantId",
                table: "Projects",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_VDisks_Resources_Id",
                table: "VDisks",
                column: "Id",
                principalTable: "Resources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_VDisks_VDisks_ParentId",
                table: "VDisks",
                column: "ParentId",
                principalTable: "VDisks",
                principalColumn: "Id");
        }
    }
}
