using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using SimpleInjector;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Host-side wiring that builds a RabbitMQ broker-user provisioner from <c>broker:*</c> configuration.
/// The split-runtime hosts that manage broker users use it — identity to create users at enrollment
/// (and its own at startup), the controller to delete them on decommission; eryph-zero never calls it,
/// so its provisioner collection stays empty and broker-user management is a no-op there.
/// </summary>
public static class ComponentBrokerProvisioning
{
    /// <summary>
    /// Appends a configuration-driven RabbitMQ provisioner to the container's
    /// <see cref="IComponentBrokerProvisioner"/> collection. Configuration is validated eagerly here so
    /// a missing/incomplete <c>broker:*</c> section fails host startup rather than the first enrollment.
    /// </summary>
    public static void AppendRabbitMq(Container container, IConfiguration configuration)
    {
        var settings = ReadSettings(configuration);
        container.Collection.Append<IComponentBrokerProvisioner>(() => Build(settings), Lifestyle.Singleton);
    }

    /// <summary>
    /// Builds a standalone RabbitMQ provisioner from configuration, for a host that must provision a
    /// broker user outside the enrollment flow (the identity service creating its own user at startup).
    /// </summary>
    public static IComponentBrokerProvisioner CreateRabbitMq(IConfiguration configuration) =>
        Build(ReadSettings(configuration));

    private static RabbitMqBrokerProvisioner Build(Settings settings)
    {
        // BaseAddress must end with '/' so the provisioner's relative "api/..." paths resolve under it;
        // the admin credential is sent as HTTP Basic auth.
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(settings.ManagementUrl.TrimEnd('/') + "/"),
        };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{settings.User}:{settings.Password}")));
        return new RabbitMqBrokerProvisioner(httpClient, settings.Options);
    }

    private static Settings ReadSettings(IConfiguration configuration)
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

        return new Settings(
            managementUrl,
            managementUser,
            managementPassword,
            new RabbitMqBrokerManagementOptions
            {
                VirtualHost = broker["virtualHost"] is { Length: > 0 } vhost ? vhost : "/",
            });
    }

    private sealed record Settings(
        string ManagementUrl, string User, string Password, RabbitMqBrokerManagementOptions Options);
}
