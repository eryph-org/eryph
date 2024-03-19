using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Eryph.Configuration.Model;
using Eryph.Modules.Controller.DataServices;
using Eryph.StateDb.Model;

namespace Eryph.Runtime.Zero.Configuration.Projects;

internal class ProjectConfigDataService :
    IConfigWriter<Project>,
    IConfigWriter<ProjectRoleAssignment>
{
    private readonly IConfigurationProvider _mapperConfig;
    private readonly string _configPath;

    public ProjectConfigDataService(IConfigurationProvider mapperConfigConfig)
    {
        _mapperConfig = mapperConfigConfig;
        _configPath = ZeroConfig.GetProjectsConfigPath();
    }

    public Task AddAsync(Project entity, CancellationToken cancellationToken = default)
    {
        return UpdateAsync(entity, cancellationToken);
    }

    public Task DeleteAsync(Project entity, CancellationToken cancellationToken = default)
    {
        File.Delete(Path.Combine(_configPath, $"{entity.Id}.json"));
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(Project entity, CancellationToken cancellationToken = default)
    {
        var configModel = _mapperConfig.CreateMapper().Map<ProjectConfigModel>(entity);
        await WriteProjectConfigAsync(configModel, cancellationToken);
    }

    public Task AddAsync(ProjectRoleAssignment entity, CancellationToken cancellationToken = default)
    {
        return UpdateAsync(entity, cancellationToken);
    }

    public async Task UpdateAsync(ProjectRoleAssignment entity, CancellationToken cancellationToken = default)
    {
        var assignmentConfig = _mapperConfig.CreateMapper().Map<ProjectRoleAssignmentConfigModel>(entity);
        var projectConfig = await ReadProjectConfigAsync(entity.ProjectId, cancellationToken);
        projectConfig.Assignments ??= Array.Empty<ProjectRoleAssignmentConfigModel>();
        projectConfig.Assignments = projectConfig.Assignments
            .Where(a => a.Id != assignmentConfig.Id)
            .Concat(new[] { assignmentConfig })
            .ToArray();
        await WriteProjectConfigAsync(projectConfig, cancellationToken);
    }

    public async Task DeleteAsync(ProjectRoleAssignment entity, CancellationToken cancellationToken = default)
    {
        var projectConfig = await ReadProjectConfigAsync(entity.ProjectId, cancellationToken);
        projectConfig.Assignments ??= Array.Empty<ProjectRoleAssignmentConfigModel>();
        projectConfig.Assignments = projectConfig.Assignments
            .Where(a => a.Id != entity.Id)
            .ToArray();
        await WriteProjectConfigAsync(projectConfig, cancellationToken);
    }

    private async Task<ProjectConfigModel> ReadProjectConfigAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(Path.Combine(_configPath, $"{projectId}.json"));
        return JsonSerializer.Deserialize<ProjectConfigModel>(json);
    }

    private async Task WriteProjectConfigAsync(ProjectConfigModel configModel, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(configModel);
        await File.WriteAllTextAsync(Path.Combine(_configPath, $"{configModel.Id}.json"), json, Encoding.UTF8);
    }
}
