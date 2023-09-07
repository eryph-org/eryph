using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Modules.Controller;
using Eryph.Resources.Machines;
using Eryph.StateDb;
using JetBrains.Annotations;
using LanguageExt;

namespace Eryph.Runtime.Zero.Configuration.VMMetadata
{
    [UsedImplicitly]
    internal class VMMetadataSeeder : IConfigSeeder<ControllerModule>
    {
        private readonly IConfigReaderService<VirtualCatletMetadata> _configReaderService;
        private readonly IStateStoreRepository<StateDb.Model.CatletMetadata> _repository;

        public VMMetadataSeeder(IConfigReaderService<VirtualCatletMetadata> configReaderService,
            IStateStoreRepository<StateDb.Model.CatletMetadata> repository)
        {
            _configReaderService = configReaderService;
            _repository = repository;
        }

        public Task Execute(CancellationToken stoppingToken)
        {
            return _configReaderService.GetConfig()
                .Map(x =>
                {
                    var json = JsonSerializer.Serialize(x);
                    return _repository.AddAsync(new StateDb.Model.CatletMetadata
                    {
                        Id = x.Id,
                        Metadata = json
                    });
                })
                .TraverseParallel(l => l).Map(_ => _repository.SaveChangesAsync(stoppingToken));
        }
    }
}