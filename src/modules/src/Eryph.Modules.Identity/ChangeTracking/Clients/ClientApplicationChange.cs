using System;

namespace Eryph.Modules.Identity.ChangeTracking.Clients;

/// <summary>A change to a client application, identified by its OpenIddict client id and tenant.</summary>
internal record ClientApplicationChange(string ClientId, Guid TenantId);
