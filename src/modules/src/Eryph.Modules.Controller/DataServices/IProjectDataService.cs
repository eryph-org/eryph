using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.StateDb.Model;

namespace Eryph.Modules.Controller.DataServices
{
    public interface IProjectDataService
    {
        public Task<Project> AddProject(Project project, CancellationToken cancellationToken = default);

        public Task UpdateProject(Project project, CancellationToken cancellationToken = default);

        public Task DeleteProject(Project project, CancellationToken cancellationToken = default);
    }
}
