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
        var baseUrl = Environment.GetEnvironmentVariable("ERYPH_API_BASEURL")
                      ?? "http://localhost:8081/";
        // Normalize so appending a path segment ("{baseUrl}compute") can't yield
        // a malformed URL when the env value is supplied without a trailing '/'.
        if (!baseUrl.EndsWith('/'))
            baseUrl += "/";
        var computeUrl = Environment.GetEnvironmentVariable("ERYPH_COMPUTE_URL")
                         ?? $"{baseUrl}compute";
        // The identity issuer used as the JWT authority. In a real deployment this is the
        // distributed/overridden identity endpoint; for the standalone dev run it is config.
        var identityUrl = Environment.GetEnvironmentVariable("ERYPH_IDENTITY_URL")
                          ?? "http://localhost:8080/identity";

        return new Dictionary<string, string>
        {
            ["base"] = baseUrl,
            ["default"] = baseUrl,
            ["compute"] = computeUrl,
            ["identity"] = identityUrl,
        };
    }
}
