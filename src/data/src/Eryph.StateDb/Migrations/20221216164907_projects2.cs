using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eryph.StateDb.Migrations
{
    public partial class projects2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Catlets_Catlets_HostId",
                table: "Catlets");

            migrationBuilder.DropForeignKey(
                name: "FK_Catlets_Projects_ProjectId",
                table: "Catlets");

            migrationBuilder.DropForeignKey(
                name: "FK_NetworkPorts_VirtualNetworks_NetworkId",
                table: "NetworkPorts");

            migrationBuilder.DropForeignKey(
                name: "FK_NetworkPorts_VirtualNetworks_RoutedNetworkId",
                table: "NetworkPorts");

            migrationBuilder.DropForeignKey(
                name: "FK_Subnet_VirtualNetworks_NetworkId",
                table: "Subnet");

            migrationBuilder.DropForeignKey(
                name: "FK_VirtualCatletNetworkAdapters_Catlets_MachineId",
                table: "VirtualCatletNetworkAdapters");

            migrationBuilder.DropForeignKey(
                name: "FK_VirtualDisks_Projects_ProjectId",
                table: "VirtualDisks");

            migrationBuilder.DropForeignKey(
                name: "FK_VirtualDisks_VirtualDisks_ParentId",
                table: "VirtualDisks");

            migrationBuilder.DropForeignKey(
                name: "FK_VirtualNetworks_Projects_ProjectId",
                table: "VirtualNetworks");

            migrationBuilder.DropTable(
                name: "VirtualCatletDrives");

            migrationBuilder.DropIndex(
                name: "IX_Catlets_HostId",
                table: "Catlets");

            migrationBuilder.DropIndex(
                name: "IX_Catlets_ProjectId",
                table: "Catlets");

            migrationBuilder.DropPrimaryKey(
                name: "PK_VirtualNetworks",
                table: "VirtualNetworks");

            migrationBuilder.DropIndex(
                name: "IX_VirtualNetworks_ProjectId",
                table: "VirtualNetworks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_VirtualDisks",
                table: "VirtualDisks");

            migrationBuilder.DropIndex(
                name: "IX_VirtualDisks_ProjectId",
                table: "VirtualDisks");

            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "Catlets");

            migrationBuilder.DropColumn(
                name: "HardwareId",
                table: "Catlets");

            migrationBuilder.DropColumn(
                name: "HostId",
                table: "Catlets");

            migrationBuilder.DropColumn(
                name: "MetadataId",
                table: "Catlets");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Catlets");

            migrationBuilder.DropColumn(
                name: "Path",
                table: "Catlets");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Catlets");

            migrationBuilder.DropColumn(
                name: "ResourceType",
                table: "Catlets");

            migrationBuilder.DropColumn(
                name: "VMId",
                table: "Catlets");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "VirtualNetworks");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "VirtualNetworks");

            migrationBuilder.DropColumn(
                name: "ResourceType",
                table: "VirtualNetworks");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "VirtualDisks");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "VirtualDisks");

            migrationBuilder.DropColumn(
                name: "ResourceType",
                table: "VirtualDisks");

            migrationBuilder.RenameTable(
                name: "VirtualNetworks",
                newName: "VNetworks");

            migrationBuilder.RenameTable(
                name: "VirtualDisks",
                newName: "VDisks");

            migrationBuilder.RenameIndex(
                name: "IX_VirtualDisks_ParentId",
                table: "VDisks",
                newName: "IX_VDisks_ParentId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_VNetworks",
                table: "VNetworks",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_VDisks",
                table: "VDisks",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Resources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Resources_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    VMId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MetadataId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: true),
                    HostId = table.Column<Guid>(type: "TEXT", nullable: true)
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
                    MachineId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: true),
                    AttachedDiskId = table.Column<Guid>(type: "TEXT", nullable: true)
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

            migrationBuilder.CreateIndex(
                name: "IX_Resources_ProjectId",
                table: "Resources",
                column: "ProjectId");

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
                name: "FK_Catlets_Resources_Id",
                table: "Catlets",
                column: "Id",
                principalTable: "Resources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NetworkPorts_VNetworks_NetworkId",
                table: "NetworkPorts",
                column: "NetworkId",
                principalTable: "VNetworks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NetworkPorts_VNetworks_RoutedNetworkId",
                table: "NetworkPorts",
                column: "RoutedNetworkId",
                principalTable: "VNetworks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Subnet_VNetworks_NetworkId",
                table: "Subnet",
                column: "NetworkId",
                principalTable: "VNetworks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

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

            migrationBuilder.AddForeignKey(
                name: "FK_VirtualCatletNetworkAdapters_VCatlets_MachineId",
                table: "VirtualCatletNetworkAdapters",
                column: "MachineId",
                principalTable: "VCatlets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_VNetworks_Resources_Id",
                table: "VNetworks",
                column: "Id",
                principalTable: "Resources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Catlets_Resources_Id",
                table: "Catlets");

            migrationBuilder.DropForeignKey(
                name: "FK_NetworkPorts_VNetworks_NetworkId",
                table: "NetworkPorts");

            migrationBuilder.DropForeignKey(
                name: "FK_NetworkPorts_VNetworks_RoutedNetworkId",
                table: "NetworkPorts");

            migrationBuilder.DropForeignKey(
                name: "FK_Subnet_VNetworks_NetworkId",
                table: "Subnet");

            migrationBuilder.DropForeignKey(
                name: "FK_VDisks_Resources_Id",
                table: "VDisks");

            migrationBuilder.DropForeignKey(
                name: "FK_VDisks_VDisks_ParentId",
                table: "VDisks");

            migrationBuilder.DropForeignKey(
                name: "FK_VirtualCatletNetworkAdapters_VCatlets_MachineId",
                table: "VirtualCatletNetworkAdapters");

            migrationBuilder.DropForeignKey(
                name: "FK_VNetworks_Resources_Id",
                table: "VNetworks");

            migrationBuilder.DropTable(
                name: "Resources");

            migrationBuilder.DropTable(
                name: "VDrives");

            migrationBuilder.DropTable(
                name: "VCatlets");

            migrationBuilder.DropTable(
                name: "VCatletHosts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_VNetworks",
                table: "VNetworks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_VDisks",
                table: "VDisks");

            migrationBuilder.RenameTable(
                name: "VNetworks",
                newName: "VirtualNetworks");

            migrationBuilder.RenameTable(
                name: "VDisks",
                newName: "VirtualDisks");

            migrationBuilder.RenameIndex(
                name: "IX_VDisks_ParentId",
                table: "VirtualDisks",
                newName: "IX_VirtualDisks_ParentId");

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "Catlets",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HardwareId",
                table: "Catlets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HostId",
                table: "Catlets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MetadataId",
                table: "Catlets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Catlets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Path",
                table: "Catlets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "Catlets",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "ResourceType",
                table: "Catlets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "VMId",
                table: "Catlets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "VirtualNetworks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "VirtualNetworks",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "ResourceType",
                table: "VirtualNetworks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "VirtualDisks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "VirtualDisks",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "ResourceType",
                table: "VirtualDisks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_VirtualNetworks",
                table: "VirtualNetworks",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_VirtualDisks",
                table: "VirtualDisks",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "VirtualCatletDrives",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    AttachedDiskId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MachineId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualCatletDrives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VirtualCatletDrives_Catlets_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Catlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VirtualCatletDrives_VirtualDisks_AttachedDiskId",
                        column: x => x.AttachedDiskId,
                        principalTable: "VirtualDisks",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Catlets_HostId",
                table: "Catlets",
                column: "HostId");

            migrationBuilder.CreateIndex(
                name: "IX_Catlets_ProjectId",
                table: "Catlets",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualNetworks_ProjectId",
                table: "VirtualNetworks",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualDisks_ProjectId",
                table: "VirtualDisks",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualCatletDrives_AttachedDiskId",
                table: "VirtualCatletDrives",
                column: "AttachedDiskId");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualCatletDrives_MachineId",
                table: "VirtualCatletDrives",
                column: "MachineId");

            migrationBuilder.AddForeignKey(
                name: "FK_Catlets_Catlets_HostId",
                table: "Catlets",
                column: "HostId",
                principalTable: "Catlets",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Catlets_Projects_ProjectId",
                table: "Catlets",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NetworkPorts_VirtualNetworks_NetworkId",
                table: "NetworkPorts",
                column: "NetworkId",
                principalTable: "VirtualNetworks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NetworkPorts_VirtualNetworks_RoutedNetworkId",
                table: "NetworkPorts",
                column: "RoutedNetworkId",
                principalTable: "VirtualNetworks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Subnet_VirtualNetworks_NetworkId",
                table: "Subnet",
                column: "NetworkId",
                principalTable: "VirtualNetworks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_VirtualCatletNetworkAdapters_Catlets_MachineId",
                table: "VirtualCatletNetworkAdapters",
                column: "MachineId",
                principalTable: "Catlets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_VirtualDisks_Projects_ProjectId",
                table: "VirtualDisks",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_VirtualDisks_VirtualDisks_ParentId",
                table: "VirtualDisks",
                column: "ParentId",
                principalTable: "VirtualDisks",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VirtualNetworks_Projects_ProjectId",
                table: "VirtualNetworks",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
