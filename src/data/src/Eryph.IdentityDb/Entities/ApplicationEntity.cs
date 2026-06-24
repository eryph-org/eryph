using System;
using OpenIddict.EntityFrameworkCore.Models;

namespace Eryph.IdentityDb.Entities;

public class ApplicationEntity : OpenIddictEntityFrameworkCoreApplication<string, AuthorizationEntity, TokenEntity>
{
    public IdentityApplicationType IdentityApplicationType { get; set; }
    public Guid TenantId { get; set; }

    public string? AppRoles { get; set; }
}
