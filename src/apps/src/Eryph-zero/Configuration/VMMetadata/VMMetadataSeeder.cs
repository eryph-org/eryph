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
        private readonly IConfigReaderService<VirtualMachineMetadata> _configReaderService;
        private readonly IStateStoreRepository<StateDb.Model.VirtualMachineMetadata> _repository;

        public VMMetadataSeeder(IConfigReaderService<VirtualMachineMetadata> configReaderService,
            IStateStoreRepository<StateDb.Model.VirtualMachineMetadata> repository)
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
                    return _repository.AddAsync(new StateDb.Model.VirtualMachineMetadata
                    {
                        Id = x.Id,
                        Metadata = json
                    });
                })
                .TraverseParallel(l => l).Map(_ => _repository.SaveChangesAsync(stoppingToken));
        }
    }
}