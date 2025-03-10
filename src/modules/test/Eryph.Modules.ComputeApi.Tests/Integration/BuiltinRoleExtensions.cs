using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core;

namespace Eryph.Modules.ComputeApi.Tests.Integration;

public static class BuiltinRoleExtensions
{
    public static Guid ToRoleId(this BuiltinRole role)
    {
        return role switch
        {
            BuiltinRole.Reader => EryphConstants.BuildInRoles.Reader,
            BuiltinRole.Contributor => EryphConstants.BuildInRoles.Contributor,
            BuiltinRole.Owner => EryphConstants.BuildInRoles.Owner,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
        };
    }
}