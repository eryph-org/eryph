using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eryph.StateDb.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Genes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GeneType = table.Column<int>(type: "INTEGER", nullable: false),
                    GeneSet = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Architecture = table.Column<string>(type: "TEXT", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenAgent = table.Column<string>(type: "TEXT", nullable: false),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    Hash = table.Column<string>(type: "TEXT", nullable: false),
                    UniqueGeneIndex = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Genes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Metadata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CatletId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VmId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SecretDataHidden = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDeprecated = table.Column<bool>(type: "INTEGER", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false)
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
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StatusMessage = table.Column<string>(type: "TEXT", nullable: true),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResultData = table.Column<string>(type: "TEXT", nullable: true),
                    ResultType = table.Column<string>(type: "TEXT", nullable: true)
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
                name: "MetadataGenes",
                columns: table => new
                {
                    MetadataId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UniqueGeneIndex = table.Column<string>(type: "TEXT", nullable: false),
                    GeneSet = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Architecture = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetadataGenes", x => new { x.MetadataId, x.UniqueGeneIndex });
                    table.ForeignKey(
                        name: "FK_MetadataGenes_Metadata_MetadataId",
                        column: x => x.MetadataId,
                        principalTable: "Metadata",
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
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationResources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationResources_Operations_OperationId",
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
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    BeingDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
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
                name: "CatletFarms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Environment = table.Column<string>(type: "TEXT", nullable: false),
                    LastInventory = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatletFarms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatletFarms_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperationProjectModel",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationProjectModel", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationProjectModel_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OperationProjectModel_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    ReferenceType = table.Column<int>(type: "INTEGER", nullable: true),
                    ReferenceId = table.Column<string>(type: "TEXT", nullable: true),
                    ReferenceProjectName = table.Column<string>(type: "TEXT", nullable: true),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true)
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
                    table.ForeignKey(
                        name: "FK_OperationTasks_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProjectRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IdentityId = table.Column<string>(type: "TEXT", nullable: false),
                    RoleId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectRoles", x => x.Id);
                    table.UniqueConstraint("AK_ProjectRoles_ProjectId_IdentityId_RoleId", x => new { x.ProjectId, x.IdentityId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_ProjectRoles_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VirtualDisks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Environment = table.Column<string>(type: "TEXT", nullable: false),
                    StorageIdentifier = table.Column<string>(type: "TEXT", nullable: true),
                    DiskIdentifier = table.Column<Guid>(type: "TEXT", nullable: false),
                    Frozen = table.Column<bool>(type: "INTEGER", nullable: false),
                    Deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: true),
                    FileName = table.Column<string>(type: "TEXT", nullable: true),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    UsedSizeBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ParentPath = table.Column<string>(type: "TEXT", nullable: true),
                    LastSeen = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenAgent = table.Column<string>(type: "TEXT", nullable: true),
                    GeneSet = table.Column<string>(type: "TEXT", nullable: true),
                    GeneName = table.Column<string>(type: "TEXT", nullable: true),
                    GeneArchitecture = table.Column<string>(type: "TEXT", nullable: true),
                    UniqueGeneIndex = table.Column<string>(type: "TEXT", nullable: true),
                    DataStore = table.Column<string>(type: "TEXT", nullable: false),
                    DiskType = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualDisks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VirtualDisks_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VirtualDisks_VirtualDisks_ParentId",
                        column: x => x.ParentId,
                        principalTable: "VirtualDisks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VirtualNetworks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Environment = table.Column<string>(type: "TEXT", nullable: false),
                    NetworkProvider = table.Column<string>(type: "TEXT", nullable: false),
                    IpNetwork = table.Column<string>(type: "TEXT", nullable: true)
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
                name: "Catlets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Environment = table.Column<string>(type: "TEXT", nullable: false),
                    AgentName = table.Column<string>(type: "TEXT", nullable: true),
                    LastSeen = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    LastSeenState = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CatletType = table.Column<int>(type: "INTEGER", nullable: false),
                    UpTime = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    VmId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MetadataId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: true),
                    StorageIdentifier = table.Column<string>(type: "TEXT", nullable: true),
                    DataStore = table.Column<string>(type: "TEXT", nullable: false),
                    Frozen = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDeprecated = table.Column<bool>(type: "INTEGER", nullable: false),
                    HostId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CpuCount = table.Column<int>(type: "INTEGER", nullable: false),
                    StartupMemory = table.Column<long>(type: "INTEGER", nullable: false),
                    MinimumMemory = table.Column<long>(type: "INTEGER", nullable: false),
                    MaximumMemory = table.Column<long>(type: "INTEGER", nullable: false),
                    SecureBootTemplate = table.Column<string>(type: "TEXT", nullable: true),
                    Features = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Catlets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Catlets_CatletFarms_HostId",
                        column: x => x.HostId,
                        principalTable: "CatletFarms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Catlets_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
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
                        name: "FK_Logs_OperationTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "OperationTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Logs_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskProgress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TaskId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "NetworkPorts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderName = table.Column<string>(type: "TEXT", nullable: true),
                    MacAddress = table.Column<string>(type: "TEXT", nullable: false),
                    AddressName = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Discriminator = table.Column<string>(type: "TEXT", maxLength: 21, nullable: false),
                    SubnetName = table.Column<string>(type: "TEXT", nullable: true),
                    PoolName = table.Column<string>(type: "TEXT", nullable: true),
                    NetworkId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FloatingPortId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CatletMetadataId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RoutedNetworkId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProviderRouterPort_SubnetName = table.Column<string>(type: "TEXT", nullable: true),
                    ProviderRouterPort_PoolName = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkPorts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkPorts_Metadata_CatletMetadataId",
                        column: x => x.CatletMetadataId,
                        principalTable: "Metadata",
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
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IpNetwork = table.Column<string>(type: "TEXT", nullable: true),
                    DnsDomain = table.Column<string>(type: "TEXT", nullable: true),
                    Discriminator = table.Column<string>(type: "TEXT", maxLength: 21, nullable: false),
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
                name: "CatletDrives",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    CatletId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    AttachedDiskId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatletDrives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatletDrives_Catlets_CatletId",
                        column: x => x.CatletId,
                        principalTable: "Catlets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CatletDrives_VirtualDisks_AttachedDiskId",
                        column: x => x.AttachedDiskId,
                        principalTable: "VirtualDisks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CatletNetworkAdapters",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    CatletId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    SwitchName = table.Column<string>(type: "TEXT", nullable: true),
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
                name: "ReportedNetworks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CatletId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MacAddress = table.Column<string>(type: "TEXT", nullable: true),
                    PortName = table.Column<string>(type: "TEXT", nullable: true),
                    IpV4Addresses = table.Column<string>(type: "TEXT", nullable: false),
                    IpV6Addresses = table.Column<string>(type: "TEXT", nullable: false),
                    IPv4DefaultGateway = table.Column<string>(type: "TEXT", nullable: true),
                    IPv6DefaultGateway = table.Column<string>(type: "TEXT", nullable: true),
                    DnsServerAddresses = table.Column<string>(type: "TEXT", nullable: false),
                    IpV4Subnets = table.Column<string>(type: "TEXT", nullable: false),
                    IpV6Subnets = table.Column<string>(type: "TEXT", nullable: false)
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
                name: "IpPools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    FirstIp = table.Column<string>(type: "TEXT", nullable: true),
                    NextIp = table.Column<string>(type: "TEXT", nullable: true),
                    LastIp = table.Column<string>(type: "TEXT", nullable: true),
                    IpNetwork = table.Column<string>(type: "TEXT", nullable: true),
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
                    NetworkPortId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Discriminator = table.Column<string>(type: "TEXT", maxLength: 21, nullable: false),
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
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CatletSpecifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Environment = table.Column<string>(type: "TEXT", nullable: false),
                    LatestId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatletSpecifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatletSpecifications_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CatletSpecificationVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SpecificationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CatletId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ConfigYaml = table.Column<string>(type: "TEXT", nullable: false),
                    IsDraft = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatletSpecificationVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatletSpecificationVersions_CatletSpecifications_SpecificationId",
                        column: x => x.SpecificationId,
                        principalTable: "CatletSpecifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CatletSpecificationVersionGenes",
                columns: table => new
                {
                    SpecificationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UniqueGeneIndex = table.Column<string>(type: "TEXT", nullable: false),
                    Hash = table.Column<string>(type: "TEXT", nullable: false),
                    GeneSet = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Architecture = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatletSpecificationVersionGenes", x => new { x.SpecificationId, x.UniqueGeneIndex });
                    table.ForeignKey(
                        name: "FK_CatletSpecificationVersionGenes_CatletSpecificationVersions_SpecificationId",
                        column: x => x.SpecificationId,
                        principalTable: "CatletSpecificationVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatletDrives_AttachedDiskId",
                table: "CatletDrives",
                column: "AttachedDiskId");

            migrationBuilder.CreateIndex(
                name: "IX_CatletDrives_CatletId",
                table: "CatletDrives",
                column: "CatletId");

            migrationBuilder.CreateIndex(
                name: "IX_CatletFarms_ProjectId",
                table: "CatletFarms",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Catlets_HostId",
                table: "Catlets",
                column: "HostId");

            migrationBuilder.CreateIndex(
                name: "IX_Catlets_ProjectId",
                table: "Catlets",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CatletSpecifications_LatestId",
                table: "CatletSpecifications",
                column: "LatestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatletSpecifications_ProjectId",
                table: "CatletSpecifications",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CatletSpecificationVersionGenes_UniqueGeneIndex",
                table: "CatletSpecificationVersionGenes",
                column: "UniqueGeneIndex",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatletSpecificationVersions_SpecificationId",
                table: "CatletSpecificationVersions",
                column: "SpecificationId");

            migrationBuilder.CreateIndex(
                name: "IX_Genes_UniqueGeneIndex_LastSeenAgent",
                table: "Genes",
                columns: new[] { "UniqueGeneIndex", "LastSeenAgent" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IpAssignment_NetworkPortId",
                table: "IpAssignment",
                column: "NetworkPortId");

            migrationBuilder.CreateIndex(
                name: "IX_IpAssignment_PoolId_Number",
                table: "IpAssignment",
                columns: new[] { "PoolId", "Number" },
                unique: true);

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
                name: "IX_MetadataGenes_UniqueGeneIndex",
                table: "MetadataGenes",
                column: "UniqueGeneIndex");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkPorts_CatletMetadataId",
                table: "NetworkPorts",
                column: "CatletMetadataId");

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
                name: "IX_OperationTasks_ProjectId",
                table: "OperationTasks",
                column: "ProjectId");

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
                name: "IX_TaskProgress_TaskId",
                table: "TaskProgress",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualDisks_ParentId",
                table: "VirtualDisks",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualDisks_ProjectId",
                table: "VirtualDisks",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualDisks_UniqueGeneIndex",
                table: "VirtualDisks",
                column: "UniqueGeneIndex");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualNetworks_ProjectId",
                table: "VirtualNetworks",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_CatletSpecifications_CatletSpecificationVersions_LatestId",
                table: "CatletSpecifications",
                column: "LatestId",
                principalTable: "CatletSpecificationVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CatletSpecifications_Projects_ProjectId",
                table: "CatletSpecifications");

            migrationBuilder.DropForeignKey(
                name: "FK_CatletSpecifications_CatletSpecificationVersions_LatestId",
                table: "CatletSpecifications");

            migrationBuilder.DropTable(
                name: "CatletDrives");

            migrationBuilder.DropTable(
                name: "CatletNetworkAdapters");

            migrationBuilder.DropTable(
                name: "CatletSpecificationVersionGenes");

            migrationBuilder.DropTable(
                name: "Genes");

            migrationBuilder.DropTable(
                name: "IpAssignment");

            migrationBuilder.DropTable(
                name: "Logs");

            migrationBuilder.DropTable(
                name: "MetadataGenes");

            migrationBuilder.DropTable(
                name: "OperationProjectModel");

            migrationBuilder.DropTable(
                name: "OperationResources");

            migrationBuilder.DropTable(
                name: "ProjectRoles");

            migrationBuilder.DropTable(
                name: "ReportedNetworks");

            migrationBuilder.DropTable(
                name: "TaskProgress");

            migrationBuilder.DropTable(
                name: "VirtualDisks");

            migrationBuilder.DropTable(
                name: "IpPools");

            migrationBuilder.DropTable(
                name: "NetworkPorts");

            migrationBuilder.DropTable(
                name: "Catlets");

            migrationBuilder.DropTable(
                name: "OperationTasks");

            migrationBuilder.DropTable(
                name: "Subnet");

            migrationBuilder.DropTable(
                name: "Metadata");

            migrationBuilder.DropTable(
                name: "CatletFarms");

            migrationBuilder.DropTable(
                name: "Operations");

            migrationBuilder.DropTable(
                name: "VirtualNetworks");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropTable(
                name: "CatletSpecificationVersions");

            migrationBuilder.DropTable(
                name: "CatletSpecifications");
        }
    }
}
