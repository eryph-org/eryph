using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.Configuration.Model;
using Eryph.StateDb;
using Eryph.StateDb.Model;

namespace Eryph.ZeroState
{
    internal class ZeroStateProjectChangeHandler : IZeroStateChangeHandler<ProjectChange>
    {
        private readonly IZeroStateConfig _config;
        private readonly IFileSystem _fileSystem;
        private readonly IStateStore _stateStore;

        public ZeroStateProjectChangeHandler(
            IZeroStateConfig config,
            IFileSystem fileSystem,
            IStateStore stateStore)
        {
            _config = config;
            _fileSystem = fileSystem;
            _stateStore = stateStore;
        }

        public async Task HandleChangeAsync(
            ProjectChange change,
            CancellationToken cancellationToken = default)
        {
            foreach (var projectId in change.ProjectIds)
            {
                var project = await _stateStore.For<Project>().GetByIdAsync(projectId, cancellationToken);
                if (project is null)
                    continue;

                await _stateStore.LoadCollectionAsync(project, p => p.ProjectRoles, cancellationToken);
                var projectConfig = new ProjectConfigModel()
                {
                    TenantId = project.TenantId,
                    Name = project.Name,
                    Assignments = project.ProjectRoles.Map(r => new ProjectRoleAssignmentConfigModel()
                    {
                        IdentityId = r.IdentityId,
                        // TODO Should we save role names?
                        RoleId = r.RoleId,
                    }).ToArray(),
                };

                var json = JsonSerializer.Serialize(projectConfig);
                await _fileSystem.File.WriteAllTextAsync(
                    Path.Combine(_config.ProjectsConfigPath, $"{projectId}.json"), 
                    json,
                    Encoding.UTF8,
                    cancellationToken);
            }
        }
    }
}
