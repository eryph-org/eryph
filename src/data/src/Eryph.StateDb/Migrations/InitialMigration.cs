using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eryph.StateDb.Migrations
{
    public partial class InitialMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Machines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AgentName = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    MachineType = table.Column<int>(type: "INTEGER", nullable: false),
                    Discriminator = table.Column<string>(type: "TEXT", nullable: false),
                    HardwareId = table.Column<string>(type: "TEXT", nullable: true),
                    VMId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MetadataId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Path = table.Column<string>(type: "TEXT", nullable: true),
                    HostId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ResourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Machines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Machines_Machines_HostId",
                        column: x => x.HostId,
                        principalTable: "Machines",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Metadata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Metadata", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StatusMessage = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Operations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subnets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsPublic = table.Column<bool>(type: "INTEGER", nullable: false),
                    DhcpEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IpVersion = table.Column<byte>(type: "INTEGER", nullable: false),
                    GatewayAddress = table.Column<string>(type: "TEXT", nullable: true),
                    Address = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subnets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VirtualDisks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StorageIdentifier = table.Column<string>(type: "TEXT", nullable: true),
                    Path = table.Column<string>(type: "TEXT", nullable: true),
                    FileName = table.Column<string>(type: "TEXT", nullable: true),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ResourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    DataStore = table.Column<string>(type: "TEXT", nullable: true),
                    Project = table.Column<string>(type: "TEXT", nullable: true),
                    Environment = table.Column<string>(type: "TEXT", nullable: true),
                    DiskType = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualDisks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VirtualDisks_VirtualDisks_ParentId",
                        column: x => x.ParentId,
                        principalTable: "VirtualDisks",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MachineNetworks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MachineId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    IpV4Addresses = table.Column<string>(type: "TEXT", nullable: true),
                    IpV6Addresses = table.Column<string>(type: "TEXT", nullable: true),
                    IPv4DefaultGateway = table.Column<string>(type: "TEXT", nullable: true),
                    IPv6DefaultGateway = table.Column<string>(type: "TEXT", nullable: true),
                    DnsServers = table.Column<string>(type: "TEXT", nullable: true),
                    IpV4Subnets = table.Column<string>(type: "TEXT", nullable: true),
                    IpV6Subnets = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineNetworks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MachineNetworks_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VirtualMachineNetworkAdapters",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    MachineId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    SwitchName = table.Column<string>(type: "TEXT", nullable: true),
                    MacAddress = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualMachineNetworkAdapters", x => new { x.MachineId, x.Id });
                    table.ForeignKey(
                        name: "FK_VirtualMachineNetworkAdapters_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OperationResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationResources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationResources_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "Operations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OperationTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    AgentName = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationTasks_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "Operations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VirtualMachineDrives",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    MachineId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: true),
                    AttachedDiskId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualMachineDrives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VirtualMachineDrives_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VirtualMachineDrives_VirtualDisks_AttachedDiskId",
                        column: x => x.AttachedDiskId,
                        principalTable: "VirtualDisks",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: true),
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TaskId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Logs_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "Operations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Logs_OperationTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "OperationTasks",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Logs_OperationId",
                table: "Logs",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_TaskId",
                table: "Logs",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineNetworks_MachineId",
                table: "MachineNetworks",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_Machines_HostId",
                table: "Machines",
                column: "HostId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationResources_OperationId",
                table: "OperationResources",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationTasks_OperationId",
                table: "OperationTasks",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_Subnets_Address",
                table: "Subnets",
                column: "Address");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualDisks_ParentId",
                table: "VirtualDisks",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualMachineDrives_AttachedDiskId",
                table: "VirtualMachineDrives",
                column: "AttachedDiskId");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualMachineDrives_MachineId",
                table: "VirtualMachineDrives",
                column: "MachineId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Logs");

            migrationBuilder.DropTable(
                name: "MachineNetworks");

            migrationBuilder.DropTable(
                name: "Metadata");

            migrationBuilder.DropTable(
                name: "OperationResources");

            migrationBuilder.DropTable(
                name: "Subnets");

            migrationBuilder.DropTable(
                name: "VirtualMachineDrives");

            migrationBuilder.DropTable(
                name: "VirtualMachineNetworkAdapters");

            migrationBuilder.DropTable(
                name: "OperationTasks");

            migrationBuilder.DropTable(
                name: "VirtualDisks");

            migrationBuilder.DropTable(
                name: "Machines");

            migrationBuilder.DropTable(
                name: "Operations");
        }
    }
}
