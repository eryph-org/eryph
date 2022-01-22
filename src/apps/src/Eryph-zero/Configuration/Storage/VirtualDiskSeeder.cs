using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Modules.Controller;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using LanguageExt;

namespace Eryph.Runtime.Zero.Configuration.Storage
{
    [UsedImplicitly]
    internal class VirtualDiskSeeder : IConfigSeeder<ControllerModule>
    {
        private readonly IConfigReaderService<VirtualDisk> _configReaderService;
        private readonly IStateStoreRepository<VirtualDisk> _repository;

        public VirtualDiskSeeder(IConfigReaderService<VirtualDisk> configReaderService,
            IStateStoreRepository<VirtualDisk> repository)
        {
            _configReaderService = configReaderService;
            _repository = repository;
        }

        public Task Execute(CancellationToken stoppingToken)
        {
            return _configReaderService.GetConfig()
                .Map(x => _repository.AddAsync(x, stoppingToken))
                .Traverse(l => l).Map(_ => _repository.SaveChangesAsync(stoppingToken));
        }
    }
}