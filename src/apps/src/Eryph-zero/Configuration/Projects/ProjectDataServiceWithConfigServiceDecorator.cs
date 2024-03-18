using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Eryph.Configuration;
using Eryph.Configuration.Model;
using Eryph.Modules.Controller.DataServices;
using Eryph.StateDb.Model;

namespace Eryph.Runtime.Zero.Configuration.Projects
{
    internal class ProjectDataServiceWithConfigServiceDecorator : IProjectDataService
    {
        private readonly IProjectDataService _decoratedService;
        private readonly ISimpleConfigWriter<Project> _configService;
        private readonly MapperConfiguration _mapperConfiguration;

        public ProjectDataServiceWithConfigServiceDecorator(
            IProjectDataService decoratedService,
            ISimpleConfigWriter<Project> configService)
        {
            _decoratedService = decoratedService;
            _configService = configService;
        }

        public async Task<Project> AddProject(Project project, CancellationToken cancellationToken = default)
        {
            var result = await _decoratedService.AddProject(project, cancellationToken);
            await _configService.Add(result);
            return result;
        }

        public async Task UpdateProject(Project project, CancellationToken cancellationToken = default)
        {
            await _decoratedService.UpdateProject(project, cancellationToken);
            await _configService.Update(project);
        }

        public async Task DeleteProject(Project project, CancellationToken cancellationToken = default)
        {
            await _decoratedService.DeleteProject(project, cancellationToken);
            await _configService.Delete(project);
        }
    }
}
