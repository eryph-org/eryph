using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;

namespace Eryph.Core;

public static class RolesNames
{
    public static string GetRoleName(Guid roleId) =>
        roleId switch
        {
            _ when roleId == EryphConstants.BuildInRoles.Owner => "owner",
            _ when roleId == EryphConstants.BuildInRoles.Contributor => "contributor",
            _ when roleId == EryphConstants.BuildInRoles.Reader => "reader",
            _ => throw new ArgumentException("Unknown role id", nameof(roleId))
        };

    public static Option<Guid> GetRoleId(string roleName) =>
        roleName switch
        {
            "owner" => EryphConstants.BuildInRoles.Owner,
            "contributor" => EryphConstants.BuildInRoles.Contributor,
            "reader" => EryphConstants.BuildInRoles.Reader,
            _ => Option<Guid>.None
        };
}
