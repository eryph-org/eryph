using System;

namespace Eryph.Core
{
    public record AuthContext(Guid TenantId, string[] Identities, Guid[] IdentityRoles);

}
