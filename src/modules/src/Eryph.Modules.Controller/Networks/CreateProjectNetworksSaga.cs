using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Messages.Resources.Networks.Commands;
using Eryph.ModuleCore.Networks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Networks;

[UsedImplicitly]
internal class CreateProjectNetworksSaga(
    ILogger log,
    IWorkflow workflow,
    INetworkProviderManager networkProviderManager,
    INetworkConfigValidator validator,
    INetworkConfigRealizer realizer,
    IStateStoreRepository<Project> projectRepository)
    :
        OperationTaskWorkflowSaga<CreateNetworksCommand, CreateProjectNetworksSagaData>(workflow),
        IHandleMessages<OperationTaskStatusEvent<UpdateNetworksCommand>>

{
    public Task Handle(OperationTaskStatusEvent<UpdateNetworksCommand> message)
    {
        return FailOrRun(message, Complete);
    }

    protected override void CorrelateMessages(ICorrelationConfig<CreateProjectNetworksSagaData> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<UpdateNetworksCommand>>(m => m.InitiatingTaskId,
            d => d.SagaTaskId);
    }


    protected override async Task Initiated(CreateNetworksCommand message)
    {
        Data.Config = validator.NormalizeConfig(message.Config);
        log.LogTrace("Update project networks. Config: {@Config}", Data.Config);

        var project = await projectRepository.GetBySpecAsync(
            new ProjectSpecs.GetById(message.TenantId, message.ProjectId));

        if (project == null)
        {
            await Fail($"The project {message.ProjectId} was not found.");
            return;
        }

        var providerConfig = await networkProviderManager.GetCurrentConfiguration()
            .Match(
                r => r,
                l =>
                {
                    l.Throw();
                    return new NetworkProvidersConfiguration();
                });

        var messages = validator.ValidateConfig(Data.Config, providerConfig.NetworkProviders).ToArray();

        if (messages.Length == 0)
            messages = await AsyncToArray(validator.ValidateChanges(project.Id, Data.Config,
                providerConfig.NetworkProviders));

        if (messages.Length == 0)
        {
            try
            {
                await realizer.UpdateNetwork(project.Id, Data.Config, providerConfig);
            }
            catch (InconsistentNetworkConfigException ex)
            {
                log.LogError(ex, "Failed to update the network. Error: {message}", ex.Message);
                await Fail(new ErrorData { ErrorMessage = $"Failed to update the network. Error: {ex.Message}" });
            }

            await StartNewTask(new UpdateNetworksCommand
            {
                Projects = [project.Id],
            });

            return;
        }

        foreach (var validationMessage in messages)
            log.LogDebug("network change validation error: {message}", validationMessage);

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
}
