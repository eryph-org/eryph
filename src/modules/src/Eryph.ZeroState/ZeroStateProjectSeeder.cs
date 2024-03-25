using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.ZeroState
{
    internal class ZeroStateProjectSeeder : ZeroStateProjectSeederBase
    {
        public ZeroStateProjectSeeder(
            IFileSystem fileSystem,
            IZeroStateConfig config) : base(fileSystem, config.ProjectNetworksConfigPath)
        {
        }

        protected override Task SeedProjectAsync(Guid projectId, string json, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
