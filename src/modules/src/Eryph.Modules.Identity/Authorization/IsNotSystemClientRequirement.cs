using Microsoft.AspNetCore.Authorization;

namespace Eryph.Modules.Identity.Authorization;

internal class IsNotSystemClientRequirement : IAuthorizationRequirement
{
}
