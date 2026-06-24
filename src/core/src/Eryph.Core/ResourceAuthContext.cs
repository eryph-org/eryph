using System;
using System.Collections.Generic;

namespace Eryph.Core;

public record AuthContext(Guid TenantId, IReadOnlyList<string> Identities, IReadOnlyList<Guid> IdentityRoles);
