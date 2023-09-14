using OpenIddict.EntityFrameworkCore.Models;

namespace Eryph.IdentityDb.Entities;

public class AuthorizationEntity : OpenIddictEntityFrameworkCoreAuthorization<string, ApplicationEntity, TokenEntity>
{
}