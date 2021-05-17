using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Haipa.Configuration;
using Haipa.Modules.Controller;
using Haipa.StateDb;
using Haipa.VmConfig;
using JetBrains.Annotations;
using LanguageExt;
using Newtonsoft.Json;

namespace Haipa.Runtime.Zero.Configuration.VMMetadata
{
    [UsedImplicitly]
    internal class VMMetadataSeeder : IConfigSeeder<ControllerModule>
    {
        private readonly IStateStoreRepository<StateDb.Model.VirtualMachineMetadata> _repository;
        private readonly IConfigReaderService<VirtualMachineMetadata> _configReaderService;

        public VMMetadataSeeder(IConfigReaderService<VirtualMachineMetadata> configReaderService, IStateStoreRepository<StateDb.Model.VirtualMachineMetadata> repository)
        {
            _configReaderService = configReaderService;
            _repository = repository;
        }

        public Task Execute(CancellationToken stoppingToken)
        {
            return _configReaderService.GetConfig()
                .Map(x =>
                {
                    var json = JsonConvert.SerializeObject(x);
                    return _repository.AddAsync(new StateDb.Model.VirtualMachineMetadata()
                    {
                        Id = x.Id,
                        Metadata = json
                    });

                })
                .Traverse(l => l).Map(_ => _repository.SaveChangesAsync());

        }

    }
}