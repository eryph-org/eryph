using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Eryph.Core;
using Eryph.Modules.AspNetCore;
using Eryph.Resources;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;
using Resource = Eryph.StateDb.Model.Resource;

namespace Eryph.Modules.ComputeApi.Tests
{
    public class UserRightsProviderTests
    {

        /// <summary>
        /// This test checks that the access rights are correctly assigned to the roles.
        /// It is just a double check as it is critical changes to roles and access rights are correct.
        /// </summary>
        /// <param name="accessRight"></param>
        /// <param name="expectedRoles"></param>
        [Theory, MemberData(nameof(GetRolesPerAccessAccessRights))]
        public void Roles_have_correct_AccessRights(AccessRight accessRight, IEnumerable<Guid> expectedRoles)
        {
            var userRightsProvider = new UserRightsProvider(null!, null!);
            var rolesForAccessRight = userRightsProvider.GetProjectRoles(accessRight);

            rolesForAccessRight.Should().BeEquivalentTo(expectedRoles);

        }

        public static IEnumerable<object[]> GetRolesPerAccessAccessRights()
        {
            yield return new object[] { AccessRight.Read, new[]
            {
                EryphConstants.BuildInRoles.Reader, 
                EryphConstants.BuildInRoles.Contributor,
                EryphConstants.BuildInRoles.Owner
            } };

            yield return new object[] { AccessRight.Write, new[]
            {
                EryphConstants.BuildInRoles.Contributor,
                EryphConstants.BuildInRoles.Owner
            } };

            yield return new object[] { AccessRight.Admin, new[]
            {
                EryphConstants.BuildInRoles.Owner
            } };
        }


        [Theory, MemberData(nameof(RolesAndAccessRights))]
        public async Task User_project_access_matches_project_role(
            AccessRight requiredAccess, Guid roleId, bool expected)
        {
            var (project, stateStore, context) = SetupMocks(roleId);
            var userRightsProvider = new UserRightsProvider(context, stateStore.Object);

            var act = await userRightsProvider.HasProjectAccess(project, requiredAccess);

            act.Should().Be(expected);
        }

        public static IEnumerable<object[]> RolesAndAccessRights()
        {
            yield return new object[] { AccessRight.Read, EryphConstants.BuildInRoles.Reader, true };
            yield return new object[] { AccessRight.Write, EryphConstants.BuildInRoles.Reader, false };
            yield return new object[] { AccessRight.Admin, EryphConstants.BuildInRoles.Reader, false };
            yield return new object[] { AccessRight.Read, EryphConstants.BuildInRoles.Contributor, true };
            yield return new object[] { AccessRight.Write, EryphConstants.BuildInRoles.Contributor, true };
            yield return new object[] { AccessRight.Admin, EryphConstants.BuildInRoles.Contributor, false };
            yield return new object[] { AccessRight.Read, EryphConstants.BuildInRoles.Owner, true };
            yield return new object[] { AccessRight.Write, EryphConstants.BuildInRoles.Owner, true };
            yield return new object[] { AccessRight.Admin, EryphConstants.BuildInRoles.Owner, true };

        }


        [Theory, MemberData(nameof(RolesAndAccessRights))]
        public async Task User_resource_access_matches_project_role(
            AccessRight requiredAccess, Guid roleId, bool expected)
        {
            var (project,stateStore, context) = SetupMocks(roleId);
            var userRightsProvider = new UserRightsProvider(context, stateStore.Object);

            var resource = new Catlet()
            {
                Id = Guid.NewGuid(),
                Project = project,
                ProjectId = project.Id,
                Name = "test",
                Environment = EryphConstants.DefaultEnvironmentName,
                DataStore = EryphConstants.DefaultDataStoreName,
                ResourceType = ResourceType.Catlet
            };

            var act = await userRightsProvider.HasResourceAccess(resource, requiredAccess);

            act.Should().Be(expected);
        }


        private static (
            Project Project,
            IMock<IStateStore> StateStore, 
            IHttpContextAccessor Context) SetupMocks(Guid roleId)
        {
            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = "test",
                TenantId = Guid.NewGuid()
            };

            var memberRole = new ProjectRoleAssignment
            {
                Id = Guid.NewGuid(),
                IdentityId = "test",
                ProjectId = project.Id,
                RoleId = roleId
            };

            var stateStore = new Mock<IStateStore>();
            var memberRepo = new Mock<IStateStoreRepository<ProjectRoleAssignment>>();
            stateStore.Setup(x => x.For<ProjectRoleAssignment>())
                .Returns(memberRepo.Object);
            memberRepo.Setup(x => x.ListAsync(
                    It.IsAny<ISpecification<ProjectRoleAssignment>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((() => new[] { memberRole }.ToList()));

            var context = new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext()
            };
            context.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, "test"),
                new Claim(ClaimTypes.NameIdentifier, "test"),
                new Claim("tid", project.TenantId.ToString())
            }));

            return (project,stateStore, context);
        }
    }
}
