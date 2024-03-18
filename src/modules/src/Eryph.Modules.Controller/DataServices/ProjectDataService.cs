using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.StateDb;
using Eryph.StateDb.Model;

namespace Eryph.Modules.Controller.DataServices
{
    internal class ProjectDataService : IProjectDataService
    {
        private readonly IStateStoreRepository<Project> _stateStore;

        public ProjectDataService(
            IStateStoreRepository<Project> stateStore)
        {
            _stateStore = stateStore;
        }

        public Task<Project> AddProject(Project project, CancellationToken cancellationToken = default)
        {
            return _stateStore.AddAsync(project, cancellationToken);
        }

        public Task UpdateProject(Project project, CancellationToken cancellationToken = default)
        {
            return _stateStore.UpdateAsync(project, cancellationToken);
        }

        public Task DeleteProject(Project project, CancellationToken cancellationToken = default)
        {
            return _stateStore.DeleteAsync(project, cancellationToken);
        }
    }
}
