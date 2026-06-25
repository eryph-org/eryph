using Eryph.Core;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Eryph.StateDb.TestBase;
using Xunit.Abstractions;

namespace Eryph.StateDb.Tests;

/// <summary>
/// Authorization rules of <see cref="OperationSpecs.GetByIdForCancellation"/>: an
/// operation may be cancelled by its requester, by an owner of all its projects, or
/// by a super admin.
/// </summary>
public class OperationCancellationSpecTests(ITestOutputHelper outputHelper)
    : InMemoryStateDbTestBase(outputHelper)
{
    private static readonly Guid TenantId = EryphConstants.DefaultTenantId;
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid OperationId = Guid.NewGuid();

    // An operation with no projects requested by RequesterId, and a pure system
    // operation with no projects and no requester.
    private static readonly Guid OwnRequesterNoProjectOpId = Guid.NewGuid();
    private static readonly Guid SystemOpId = Guid.NewGuid();

    private const string RequesterId = "the-requester";
    private const string OwnerId = "the-owner";
    private const string StrangerId = "a-stranger";

    private static readonly Guid[] OwnerRoles = [EryphConstants.BuildInRoles.Owner];

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await stateStore.For<Tenant>().AddAsync(new Tenant { Id = TenantId });

        await stateStore.For<Project>().AddAsync(new Project
        {
            Id = ProjectId,
            TenantId = TenantId,
            Name = "test-project",
            ProjectRoles =
            [
                new ProjectRoleAssignment
                {
                    Id = Guid.NewGuid(),
                    IdentityId = OwnerId,
                    RoleId = EryphConstants.BuildInRoles.Owner,
                },
            ],
        });

        await stateStore.For<OperationModel>().AddAsync(new OperationModel
        {
            Id = OperationId,
            TenantId = TenantId,
            RequestedBy = RequesterId,
            Projects = [new OperationProjectModel { Id = Guid.NewGuid(), ProjectId = ProjectId }],
        });

        await stateStore.For<OperationModel>().AddAsync(new OperationModel
        {
            Id = OwnRequesterNoProjectOpId,
            TenantId = TenantId,
            RequestedBy = RequesterId,
            Projects = [],
        });

        await stateStore.For<OperationModel>().AddAsync(new OperationModel
        {
            Id = SystemOpId,
            TenantId = TenantId,
            RequestedBy = null,
            Projects = [],
        });
    }

    [Theory]
    [InlineData(RequesterId)] // the requester may cancel their own operation
    [InlineData(OwnerId)]     // a project owner may cancel
    public async Task Authorized_caller_finds_the_operation(string identity)
    {
        await using var scope = CreateScope();
        var stateStore = scope.GetInstance<IStateStore>();

        var authContext = new AuthContext(TenantId, [identity], []);
        var result = await stateStore.For<OperationModel>().GetBySpecAsync(
            new OperationSpecs.GetByIdForCancellation(OperationId, authContext, identity, OwnerRoles));

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Unrelated_caller_does_not_find_the_operation()
    {
        await using var scope = CreateScope();
        var stateStore = scope.GetInstance<IStateStore>();

        var authContext = new AuthContext(TenantId, [StrangerId], []);
        var result = await stateStore.For<OperationModel>().GetBySpecAsync(
            new OperationSpecs.GetByIdForCancellation(OperationId, authContext, StrangerId, OwnerRoles));

        result.Should().BeNull();
    }

    [Fact]
    public async Task Requester_can_cancel_their_own_operation_without_projects()
    {
        await using var scope = CreateScope();
        var stateStore = scope.GetInstance<IStateStore>();

        var authContext = new AuthContext(TenantId, [RequesterId], []);
        var result = await stateStore.For<OperationModel>().GetBySpecAsync(
            new OperationSpecs.GetByIdForCancellation(OwnRequesterNoProjectOpId, authContext, RequesterId, OwnerRoles));

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Stranger_cannot_cancel_an_operation_without_projects()
    {
        await using var scope = CreateScope();
        var stateStore = scope.GetInstance<IStateStore>();

        var authContext = new AuthContext(TenantId, [StrangerId], []);
        var result = await stateStore.For<OperationModel>().GetBySpecAsync(
            new OperationSpecs.GetByIdForCancellation(OwnRequesterNoProjectOpId, authContext, StrangerId, OwnerRoles));

        result.Should().BeNull();
    }

    [Fact]
    public async Task Non_admin_cannot_cancel_a_projectless_system_operation()
    {
        // Guards against the vacuous truth of "all projects are owned" when there are
        // no projects: a system operation (no requester, no projects) must not be
        // cancellable by an ordinary user.
        await using var scope = CreateScope();
        var stateStore = scope.GetInstance<IStateStore>();

        var authContext = new AuthContext(TenantId, [StrangerId], []);
        var result = await stateStore.For<OperationModel>().GetBySpecAsync(
            new OperationSpecs.GetByIdForCancellation(SystemOpId, authContext, StrangerId, OwnerRoles));

        result.Should().BeNull();
    }

    [Fact]
    public async Task Super_admin_finds_the_operation()
    {
        await using var scope = CreateScope();
        var stateStore = scope.GetInstance<IStateStore>();

        var authContext = new AuthContext(TenantId, [StrangerId], [EryphConstants.SuperAdminRole]);
        var result = await stateStore.For<OperationModel>().GetBySpecAsync(
            new OperationSpecs.GetByIdForCancellation(OperationId, authContext, StrangerId, OwnerRoles));

        result.Should().NotBeNull();
    }
}
