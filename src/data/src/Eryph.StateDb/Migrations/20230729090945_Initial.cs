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
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false, defaultValue: new Guid("c1813384-8ecb-4f17-b846-821ee515d19b")),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StatusMessage = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Operations", x => x.Id);
                });

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
                    ParentTaskId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: false),
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
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TaskId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Logs_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Logs_OperationTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "OperationTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OperationProjectModel",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationProjectModel", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationProjectModel_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "Operations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OperationProjectModel_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "Catlets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AgentName = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CatletType = table.Column<int>(type: "INTEGER", nullable: false),
                    UpTime = table.Column<TimeSpan>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Catlets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Catlets_Resources_Id",
                        column: x => x.Id,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VDisks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StorageIdentifier = table.Column<string>(type: "TEXT", nullable: true),
                    Path = table.Column<string>(type: "TEXT", nullable: true),
                    FileName = table.Column<string>(type: "TEXT", nullable: true),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DataStore = table.Column<string>(type: "TEXT", nullable: true),
                    Environment = table.Column<string>(type: "TEXT", nullable: true),
                    DiskType = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VDisks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VDisks_Resources_Id",
                        column: x => x.Id,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VDisks_VDisks_ParentId",
                        column: x => x.ParentId,
                        principalTable: "VDisks",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VNetworks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    NetworkProvider = table.Column<string>(type: "TEXT", nullable: true),
                    IpNetwork = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VNetworks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VNetworks_Resources_Id",
                        column: x => x.Id,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "NetworkPorts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderName = table.Column<string>(type: "TEXT", nullable: true),
                    MacAddress = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Discriminator = table.Column<string>(type: "TEXT", nullable: false),
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
                        name: "FK_NetworkPorts_VNetworks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "VNetworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NetworkPorts_VNetworks_RoutedNetworkId",
                        column: x => x.RoutedNetworkId,
                        principalTable: "VNetworks",
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
                        name: "FK_Subnet_VNetworks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "VNetworks",
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
                    HostId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CpuCount = table.Column<int>(type: "INTEGER", nullable: false),
                    StartupMemory = table.Column<long>(type: "INTEGER", nullable: false),
                    MinimumMemory = table.Column<long>(type: "INTEGER", nullable: false),
                    MaximumMemory = table.Column<long>(type: "INTEGER", nullable: false),
                    SecureBootTemplate = table.Column<string>(type: "TEXT", nullable: true),
                    Features = table.Column<string>(type: "TEXT", nullable: true)
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
                        name: "FK_VirtualCatletNetworkAdapters_VCatlets_MachineId",
                        column: x => x.MachineId,
                        principalTable: "VCatlets",
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
                name: "IX_OperationProjectModel_OperationId",
                table: "OperationProjectModel",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationProjectModel_ProjectId",
                table: "OperationProjectModel",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationResources_OperationId",
                table: "OperationResources",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationTasks_OperationId",
                table: "OperationTasks",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectProjectRoles_RolesRoleId_RolesProjectId",
                table: "ProjectProjectRoles",
                columns: new[] { "RolesRoleId", "RolesProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_TenantId",
                table: "Projects",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportedNetworks_CatletId",
                table: "ReportedNetworks",
                column: "CatletId");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_ProjectId",
                table: "Resources",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Subnet_NetworkId",
                table: "Subnet",
                column: "NetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_VCatlets_HostId",
                table: "VCatlets",
                column: "HostId");

            migrationBuilder.CreateIndex(
                name: "IX_VDisks_ParentId",
                table: "VDisks",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_VDrives_AttachedDiskId",
                table: "VDrives",
                column: "AttachedDiskId");

            migrationBuilder.CreateIndex(
                name: "IX_VDrives_MachineId",
                table: "VDrives",
                column: "MachineId");
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
                name: "OperationProjectModel");

            migrationBuilder.DropTable(
                name: "OperationResources");

            migrationBuilder.DropTable(
                name: "ProjectProjectRoles");

            migrationBuilder.DropTable(
                name: "ReportedNetworks");

            migrationBuilder.DropTable(
                name: "VDrives");

            migrationBuilder.DropTable(
                name: "VirtualCatletNetworkAdapters");

            migrationBuilder.DropTable(
                name: "IpPools");

            migrationBuilder.DropTable(
                name: "NetworkPorts");

            migrationBuilder.DropTable(
                name: "OperationTasks");

            migrationBuilder.DropTable(
                name: "ProjectRoles");

            migrationBuilder.DropTable(
                name: "VDisks");

            migrationBuilder.DropTable(
                name: "VCatlets");

            migrationBuilder.DropTable(
                name: "Subnet");

            migrationBuilder.DropTable(
                name: "Operations");

            migrationBuilder.DropTable(
                name: "VCatletHosts");

            migrationBuilder.DropTable(
                name: "VNetworks");

            migrationBuilder.DropTable(
                name: "Catlets");

            migrationBuilder.DropTable(
                name: "Resources");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "Tenants");
        }
    }
}
