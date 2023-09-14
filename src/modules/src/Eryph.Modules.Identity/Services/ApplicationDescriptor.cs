using System;
using System.Collections.Generic;
using Eryph.IdentityDb;
using OpenIddict.Abstractions;

namespace Eryph.Modules.Identity.Services;

public class ApplicationDescriptor : OpenIddictApplicationDescriptor, ICloneable
{
    /// <summary>
    /// Gets or sets the application type of the application. The default value <see cref="IdentityApplicationType.OAuth"/> is a standard
    /// oauth application. The value <see cref="IdentityApplicationType.Client"/> is a eryph client.
    /// </summary>
    public IdentityApplicationType IdentityApplicationType { get; set; }

    /// <summary>
    /// Tenant identifier associated with the application.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Gets the Roles associated with the application.
    /// </summary>
    public HashSet<Guid> AppRoles { get; } = new();


    /// <summary>
    /// Gets the Scopes associated with the application.
    /// </summary>
    public HashSet<string> Scopes { get; } = new(StringComparer.Ordinal);

    object ICloneable.Clone()
    {
        return Clone<ApplicationDescriptor>();
    }

    public virtual TDescriptor Clone<TDescriptor>() where TDescriptor : ApplicationDescriptor, new()
    {
        var copy = new TDescriptor
        {
            ClientId = ClientId,
            ClientSecret = ClientSecret,
            ConsentType = ConsentType,
            DisplayName = DisplayName,
            Type = Type,
            IdentityApplicationType = IdentityApplicationType,
            TenantId = TenantId,
        };

        foreach (var displayName in DisplayNames)
        {
            copy.DisplayNames.Add(displayName.Key, displayName.Value);
        }

        foreach (var property in Properties)
        {
            copy.Properties.Add(property.Key, property.Value);
        }

        copy.Permissions.UnionWith(Permissions);
        copy.AppRoles.UnionWith(AppRoles);
        copy.Scopes.UnionWith(Scopes);
        copy.RedirectUris.UnionWith(RedirectUris);
        copy.PostLogoutRedirectUris.UnionWith(PostLogoutRedirectUris);
        copy.Requirements.UnionWith(Requirements);

        return copy;

    }

}