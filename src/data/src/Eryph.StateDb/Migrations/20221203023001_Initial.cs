using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eryph.StateDb.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
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
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
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
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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

            migrationBuilder.CreateTable(
                name: "Catlets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AgentName = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CatletType = table.Column<int>(type: "INTEGER", nullable: false),
                    UpTime = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    Discriminator = table.Column<string>(type: "TEXT", nullable: false),
                    VMId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MetadataId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Path = table.Column<string>(type: "TEXT", nullable: true),
                    HostId = table.Column<Guid>(type: "TEXT", nullable: true),
                    HardwareId = table.Column<string>(type: "TEXT", nullable: true),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Catlets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Catlets_Catlets_HostId",
                        column: x => x.HostId,
                        principalTable: "Catlets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Catlets_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VirtualNetworks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    NetworkProvider = table.Column<string>(type: "TEXT", nullable: true),
                    IpNetwork = table.Column<string>(type: "TEXT", nullable: true),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualNetworks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VirtualNetworks_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportedNetworks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CatletId = table.Column<Guid>(type: "TEXT", nullable: false),
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
                    table.PrimaryKey("PK_ReportedNetworks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportedNetworks_Catlets_CatletId",
                        column: x => x.CatletId,
                        principalTable: "Catlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VirtualCatletDrives",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    MachineId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: true),
                    AttachedDiskId = table.Column<Guid>(type: "TEXT", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "VirtualCatletNetworkAdapters",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    MachineId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    SwitchName = table.Column<string>(type: "TEXT", nullable: true),
                    NetworkProviderName = table.Column<string>(type: "TEXT", nullable: true),
                    MacAddress = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualCatletNetworkAdapters", x => new { x.MachineId, x.Id });
                    table.ForeignKey(
                        name: "FK_VirtualCatletNetworkAdapters_Catlets_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Catlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NetworkPorts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderName = table.Column<string>(type: "TEXT", nullable: true),
                    MacAddress = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Discriminator = table.Column<string>(type: "TEXT", nullable: false),
                    AssignedPortId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SubnetName = table.Column<string>(type: "TEXT", nullable: true),
                    PoolName = table.Column<string>(type: "TEXT", nullable: true),
                    NetworkId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FloatingPortId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CatletId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RoutedNetworkId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkPorts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkPorts_Catlets_CatletId",
                        column: x => x.CatletId,
                        principalTable: "Catlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NetworkPorts_NetworkPorts_FloatingPortId",
                        column: x => x.FloatingPortId,
                        principalTable: "NetworkPorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_NetworkPorts_VirtualNetworks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "VirtualNetworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NetworkPorts_VirtualNetworks_RoutedNetworkId",
                        column: x => x.RoutedNetworkId,
                        principalTable: "VirtualNetworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subnet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    IpNetwork = table.Column<string>(type: "TEXT", nullable: true),
                    Discriminator = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderName = table.Column<string>(type: "TEXT", nullable: true),
                    NetworkId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DhcpLeaseTime = table.Column<int>(type: "INTEGER", nullable: true),
                    MTU = table.Column<int>(type: "INTEGER", nullable: true),
                    DnsServersV4 = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subnet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subnet_VirtualNetworks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "VirtualNetworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IpPools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    FirstIp = table.Column<string>(type: "TEXT", nullable: true),
                    LastIp = table.Column<string>(type: "TEXT", nullable: true),
                    IpNetwork = table.Column<string>(type: "TEXT", nullable: true),
                    Counter = table.Column<int>(type: "INTEGER", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true),
                    SubnetId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IpPools", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IpPools_Subnet_SubnetId",
                        column: x => x.SubnetId,
                        principalTable: "Subnet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IpAssignment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubnetId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IpAddress = table.Column<string>(type: "TEXT", nullable: true),
                    NetworkPortId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Discriminator = table.Column<string>(type: "TEXT", nullable: false),
                    PoolId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Number = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IpAssignment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IpAssignment_IpPools_PoolId",
                        column: x => x.PoolId,
                        principalTable: "IpPools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IpAssignment_NetworkPorts_NetworkPortId",
                        column: x => x.NetworkPortId,
                        principalTable: "NetworkPorts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IpAssignment_Subnet_SubnetId",
                        column: x => x.SubnetId,
                        principalTable: "Subnet",
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
                name: "IX_IpAssignment_NetworkPortId",
                table: "IpAssignment",
                column: "NetworkPortId");

            migrationBuilder.CreateIndex(
                name: "IX_IpAssignment_PoolId",
                table: "IpAssignment",
                column: "PoolId");

            migrationBuilder.CreateIndex(
                name: "IX_IpAssignment_SubnetId",
                table: "IpAssignment",
                column: "SubnetId");

            migrationBuilder.CreateIndex(
                name: "IX_IpPools_SubnetId",
                table: "IpPools",
                column: "SubnetId");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_OperationId",
                table: "Logs",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_TaskId",
                table: "Logs",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkPorts_CatletId",
                table: "NetworkPorts",
                column: "CatletId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkPorts_FloatingPortId",
                table: "NetworkPorts",
                column: "FloatingPortId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkPorts_MacAddress",
                table: "NetworkPorts",
                column: "MacAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkPorts_NetworkId",
                table: "NetworkPorts",
                column: "NetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkPorts_RoutedNetworkId",
                table: "NetworkPorts",
                column: "RoutedNetworkId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationResources_OperationId",
                table: "OperationResources",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationTasks_OperationId",
                table: "OperationTasks",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_TenantId",
                table: "Projects",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportedNetworks_CatletId",
                table: "ReportedNetworks",
                column: "CatletId");

            migrationBuilder.CreateIndex(
                name: "IX_Subnet_NetworkId",
                table: "Subnet",
                column: "NetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualCatletDrives_AttachedDiskId",
                table: "VirtualCatletDrives",
                column: "AttachedDiskId");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualCatletDrives_MachineId",
                table: "VirtualCatletDrives",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualDisks_ParentId",
                table: "VirtualDisks",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualNetworks_ProjectId",
                table: "VirtualNetworks",
                column: "ProjectId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IpAssignment");

            migrationBuilder.DropTable(
                name: "Logs");

            migrationBuilder.DropTable(
                name: "Metadata");

            migrationBuilder.DropTable(
                name: "OperationResources");

            migrationBuilder.DropTable(
                name: "ReportedNetworks");

            migrationBuilder.DropTable(
                name: "VirtualCatletDrives");

            migrationBuilder.DropTable(
                name: "VirtualCatletNetworkAdapters");

            migrationBuilder.DropTable(
                name: "IpPools");

            migrationBuilder.DropTable(
                name: "NetworkPorts");

            migrationBuilder.DropTable(
                name: "OperationTasks");

            migrationBuilder.DropTable(
                name: "VirtualDisks");

            migrationBuilder.DropTable(
                name: "Subnet");

            migrationBuilder.DropTable(
                name: "Catlets");

            migrationBuilder.DropTable(
                name: "Operations");

            migrationBuilder.DropTable(
                name: "VirtualNetworks");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "Tenants");
        }
    }
}
