using Eryph.IdentityDb.Entities;
using OpenIddict.EntityFrameworkCore.Models;

namespace Eryph.IdentityDb;

public class TokenEntity : OpenIddictEntityFrameworkCoreToken<string, ApplicationEntity, AuthorizationEntity>
{
}