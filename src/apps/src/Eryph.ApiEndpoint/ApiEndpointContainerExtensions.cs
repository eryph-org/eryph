using System;
using System.Collections.Generic;
using Dbosoft.Rebus.Operations;
using Eryph.ModuleCore;
using Eryph.Rebus;
using Eryph.StateDb;
using Eryph.StateDb.MySql;
using SimpleInjector;

namespace Eryph.ApiEndpoint;

internal static class ApiEndpointContainerExtensions
{
    /// <summary>
    /// Root-container registrations the <see cref="Eryph.Modules.ComputeApi.ComputeApiModule"/>
    /// (and its <c>ApiModule</c> base) resolve through the cross-wired provider: the shared
    /// state store (read side), the endpoint resolver (its own compute URL plus the identity
    /// issuer for JWT), and the workflow options used to dispatch operations to the controller.
    /// </summary>
    public static void Bootstrap(this Container container)
    {
        // The compute API reads the same state database the controller owns; the controller
        // applies the migrations, so the API only needs the connection.
        container.RegisterInstance<IStateStoreContextConfigurer>(
            new MySqlStateStoreContextConfigurer(GetStateDbConnectionString()));

        container.RegisterInstance<IEndpointResolver>(new EndpointResolver(GetEndpoints()));

        container.RegisterInstance(new WorkflowOptions
        {
            DispatchMode = WorkflowEventDispatchMode.Publish,
            EventDestination = QueueNames.Controllers,
            OperationsDestination = QueueNames.Controllers,
            DeferCompletion = TimeSpan.FromMinutes(1),
            JsonSerializerOptions = EryphJsonSerializerOptions.Options,
        });
    }

    public static string GetStateDbConnectionString() =>
        Environment.GetEnvironmentVariable("ERYPH_STATEDB_CONNECTIONSTRING")
        ?? throw new InvalidOperationException(
            "The state database connection string must be provided via the "
            + "ERYPH_STATEDB_CONNECTIONSTRING environment variable.");

    private static Dictionary<string, string> GetEndpoints()
    {
        // The compute API's own public access URL (endpoints:public) — its advertised endpoint and
        // module path, and the host baked into its enrolled server certificate. Independent of
        // ASPNETCORE_URLS (the bind address), so a load balancer can front it. It serves at the root of
        // its own host, so the URL carries no path prefix.
        var accessUrl = (ComponentPublicEndpoint.GetFromEnvironment()
            ?? throw new InvalidOperationException(
                "endpoints:public must be set to the compute API access URL.")).ToString();

        // The identity access URL used as the JWT authority — a consumer pointer to another component,
        // so it stays its own value (the identity's endpoints:public, addressed at its host root).
        var identityUrl = Environment.GetEnvironmentVariable("ERYPH_IDENTITY_URL")
            ?? throw new InvalidOperationException(
                "ERYPH_IDENTITY_URL must be set to the identity access URL (the JWT authority).");

        return new Dictionary<string, string>
        {
            ["base"] = accessUrl,
            ["default"] = accessUrl,
            ["compute"] = accessUrl,
            ["identity"] = identityUrl,
        };
    }
}
