using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Messages;
using Eryph.Messages.Operations.Events;
using Eryph.Messages.Resources.Networks.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.Operations;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Networks
{
    [UsedImplicitly]
    internal class CreateProjectNetworksSaga : OperationTaskWorkflowSaga<CreateNetworksCommand, CreateProjectNetworksSagaData>,
        IHandleMessages<OperationTaskStatusEvent<UpdateNetworksCommand>>

    {
        private readonly INetworkConfigValidator _validator;
        private readonly INetworkConfigRealizer _realizer;
        private readonly IStateStoreRepository<Project> _projectRepository;
        private readonly ILogger _log;
        private readonly INetworkProviderManager _networkProviderManager;

        public CreateProjectNetworksSaga(IBus bus, IOperationTaskDispatcher taskDispatcher, ILogger log, 
            INetworkProviderManager networkProviderManager, 
            INetworkConfigValidator validator, 
            INetworkConfigRealizer realizer, 
            IStateStoreRepository<Project> projectRepository) : base(bus, taskDispatcher)
        {
            _log = log;
            _networkProviderManager = networkProviderManager;
            _validator = validator;
            _realizer = realizer;
            _projectRepository = projectRepository;
        }

        protected override void CorrelateMessages(ICorrelationConfig<CreateProjectNetworksSagaData> config)
        {
            base.CorrelateMessages(config);

            config.Correlate<OperationTaskStatusEvent<UpdateNetworksCommand>>(m => m.InitiatingTaskId, d => d.SagaTaskId);
        }


        protected override async Task Initiated(CreateNetworksCommand message)
        {

            Data.Config = _validator.NormalizeConfig(message.Config);
            _log.LogTrace("Update project networks. Config: {@Config}", Data.Config);
            
            var project = await _projectRepository.GetBySpecAsync(
                new ProjectSpecs.GetByName(
                    EryphConstants.DefaultTenantId, Data.Config.Project ?? "default"));

            if (project == null)
            {
                await Fail(new ErrorData { ErrorMessage = $"Project {Data.Config.Project} not found" });
                return;
            }

            var providerConfig = await _networkProviderManager.GetCurrentConfiguration()
                .Match(
                    r=> r,
                    l =>
                    {
                        l.Throw();
                        return new NetworkProvidersConfiguration();
                    });

            var messages = _validator.ValidateConfig(Data.Config,providerConfig).ToArray();

            if (messages.Length == 0) 
                messages = await AsyncToArray(_validator.ValidateChanges(project.Id, Data.Config, providerConfig));

            if (messages.Length == 0)
            {
                await _realizer.UpdateNetwork(project.Id, Data.Config, providerConfig);

                await StartNewTask(new UpdateNetworksCommand
                {
                    Projects = new[] { project.Id }
                });

                return;
            }

            foreach (var validationMessage in messages)
            {
                _log.LogDebug("network change validation error: {message}", validationMessage);
            }

            await Fail(new ErrorData { ErrorMessage = string.Join('\n', messages) });
        }

        private static async Task<T[]> AsyncToArray<T>(IAsyncEnumerable<T> items)
        {
            var results = new List<T>();
            await foreach (var item in items
                               .ConfigureAwait(false))
                results.Add(item);
            return results.ToArray();
        }

 
        public Task Handle(OperationTaskStatusEvent<UpdateNetworksCommand> message)
        {
            return FailOrRun(message, () => Complete());
        }
    }
}