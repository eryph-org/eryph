using OpenIddict.EntityFrameworkCore.Models;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel;
using OpenIddict.Abstractions;
using System.Linq;
using System.Text.Json;

namespace Eryph.IdentityDb.Entities;

public class ApplicationEntity : OpenIddictEntityFrameworkCoreApplication<string, AuthorizationEntity, TokenEntity>
{
    public IdentityApplicationType IdentityApplicationType { get; set; }
    public Guid TenantId { get; set; }

    public string AppRoles { get; set; }


}