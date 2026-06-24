using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using SimpleInjector;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Host-side wiring that appends a RabbitMQ broker-user provisioner (built from <c>broker:*</c>
/// configuration) to a module container's <see cref="IComponentBrokerProvisioner"/> collection. The
/// split-runtime hosts that manage broker users call this — identity to create users at enrollment,
/// the controller to delete them on decommission; eryph-zero never calls it, so its collection stays
/// empty and broker-user management is a no-op there.
/// </summary>
public static class ComponentBrokerProvisioning
{
    public static void AppendRabbitMq(Container container, IConfiguration configuration)
    {
        var broker = configuration.GetSection("broker");

        // Configuration is required (no silent fallback): the management endpoint and an admin
        // credential are the operator's contract for managing per-component broker users.
        var managementUrl = broker["managementUrl"];
        if (string.IsNullOrWhiteSpace(managementUrl))
            throw new InvalidOperationException(
                "broker:managementUrl must be set so the split runtime can manage per-component broker users.");
        var managementUser = broker["managementUser"];
        var managementPassword = broker["managementPassword"];
        if (string.IsNullOrWhiteSpace(managementUser) || string.IsNullOrWhiteSpace(managementPassword))
            throw new InvalidOperationException(
                "broker:managementUser and broker:managementPassword must be set to authenticate to the "
                + "RabbitMQ management API.");

        var options = new RabbitMqBrokerManagementOptions
        {
            VirtualHost = broker["virtualHost"] is { Length: > 0 } vhost ? vhost : "/",
        };

        container.Collection.Append<IComponentBrokerProvisioner>(
            () =>
            {
                // BaseAddress must end with '/' so the provisioner's relative "api/..." paths resolve
                // under it; the admin credential is sent as HTTP Basic auth.
                var httpClient = new HttpClient
                {
                    BaseAddress = new Uri(managementUrl.TrimEnd('/') + "/"),
                };
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(Encoding.ASCII.GetBytes($"{managementUser}:{managementPassword}")));
                return new RabbitMqBrokerProvisioner(httpClient, options);
            },
            Lifestyle.Singleton);
    }
}
