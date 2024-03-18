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

internal class ProjectConfigDataService : IConfigWriter<Project>
{
    private readonly IConfigurationProvider _mapperConfig;
    private readonly string _configPath;

    public ProjectConfigDataService(IConfigurationProvider mapperConfigConfig)
    {
        _mapperConfig = mapperConfigConfig;
        _configPath = ZeroConfig.GetProjectsConfigPath();
    }

    public async Task AddAsync(Project entity, CancellationToken cancellationToken)
    {
        var configModel = _mapperConfig.CreateMapper().Map<ProjectConfigModel>(entity);
        var json = JsonSerializer.Serialize(configModel);
        await File.WriteAllTextAsync(Path.Combine(_configPath, $"{entity.Id}.json"), json, Encoding.UTF8, cancellationToken);
        
    }
    public Task DeleteAsync(Project entity, CancellationToken cancellationToken)
    {
        File.Delete(Path.Combine(_configPath, $"{entity.Id}.json"));
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(Project entity, CancellationToken cancellationToken)
    {
        var configModel = _mapperConfig.CreateMapper().Map<ProjectConfigModel>(entity);
        var json = JsonSerializer.Serialize(configModel);
        await File.WriteAllTextAsync(Path.Combine(_configPath, $"{entity.Id}.json"), json, Encoding.UTF8, cancellationToken);
    }
}
