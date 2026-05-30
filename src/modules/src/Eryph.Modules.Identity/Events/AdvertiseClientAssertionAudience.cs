using System;
using System.Threading.Tasks;
using OpenIddict.Server;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace Eryph.Modules.Identity.Events;

/// <summary>
/// Adds a custom entry to the OpenID Connect discovery document (<c>.well-known/openid-configuration</c>)
/// advertising that this authorization server expects the <c>issuer</c> as the audience of client
/// assertions (<c>private_key_jwt</c>), as required by OpenIddict 7.0 and higher.
/// <para>
/// Clients that understand this flag use the issuer as the client-assertion audience and the
/// <c>client-authentication+jwt</c> token type. Clients talking to older eryph servers — where the
/// flag is absent — keep using the legacy token-endpoint audience, so a single up-to-date client
/// library works against both old and new servers.
/// </para>
/// </summary>
public sealed class AdvertiseClientAssertionAudience : IOpenIddictServerHandler<HandleConfigurationRequestContext>
{
    /// <summary>
    /// Name of the discovery metadata entry. Kept in sync with the eryph client libraries.
    /// </summary>
    public const string MetadataName = "eryph_client_assertion_audience";

    /// <summary>
    /// Value indicating that the issuer is expected as the client-assertion audience.
    /// </summary>
    public const string AudienceIssuer = "issuer";

    public static OpenIddictServerHandlerDescriptor Descriptor { get; }
        = OpenIddictServerHandlerDescriptor.CreateBuilder<HandleConfigurationRequestContext>()
            .UseSingletonHandler<AdvertiseClientAssertionAudience>()
            .SetOrder(int.MaxValue - 100_000)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public ValueTask HandleAsync(HandleConfigurationRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Metadata[MetadataName] = AudienceIssuer;

        return ValueTask.CompletedTask;
    }
}
