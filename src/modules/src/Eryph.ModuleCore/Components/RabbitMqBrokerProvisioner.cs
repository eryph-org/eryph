using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Provisions per-component users on RabbitMQ through its management HTTP API. The user is created
/// with no password (an empty <c>password_hash</c>), so it can only authenticate with SASL EXTERNAL
/// via the component's certificate — never with a password. The <see cref="HttpClient"/> supplies the
/// management base address and admin credentials (Basic auth).
/// </summary>
public sealed class RabbitMqBrokerProvisioner(
    HttpClient httpClient, RabbitMqBrokerManagementOptions options)
    : IComponentBrokerProvisioner
{
    public async Task EnsureComponentAsync(Guid componentId, CancellationToken cancellationToken = default)
    {
        var user = ComponentBrokerIdentity.UserName(componentId);

        // No password (empty password_hash) and no tags: the user exists only to authenticate the
        // component's certificate via EXTERNAL, with no management access and no password login.
        using (var response = await httpClient.PutAsync(
                   UserPath(user),
                   JsonContent(new { password_hash = "", tags = "" }),
                   cancellationToken))
        {
            response.EnsureSuccessStatusCode();
        }

        using (var response = await httpClient.PutAsync(
                   PermissionsPath(user),
                   JsonContent(new
                   {
                       configure = options.ConfigurePermission,
                       write = options.WritePermission,
                       read = options.ReadPermission,
                   }),
                   cancellationToken))
        {
            response.EnsureSuccessStatusCode();
        }
    }

    public async Task RemoveComponentAsync(Guid componentId, CancellationToken cancellationToken = default)
    {
        var user = ComponentBrokerIdentity.UserName(componentId);
        using var response = await httpClient.DeleteAsync(UserPath(user), cancellationToken);

        // Deleting an already-absent user is success for an idempotent revoke: the end state (no user)
        // is what matters, so a 404 must not surface as a failure.
        if (response.StatusCode == HttpStatusCode.NotFound)
            return;
        response.EnsureSuccessStatusCode();
    }

    // The user name is the component-id URN (contains ':'); it must be percent-encoded into the path.
    private static string UserPath(string user) => "api/users/" + Uri.EscapeDataString(user);

    private string PermissionsPath(string user) =>
        $"api/permissions/{Uri.EscapeDataString(options.VirtualHost)}/{Uri.EscapeDataString(user)}";

    private static StringContent JsonContent(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
}
