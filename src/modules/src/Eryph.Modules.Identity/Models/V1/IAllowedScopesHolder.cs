using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.Identity.Models.V1;

public interface IAllowedScopesHolder
{
    /// <summary>
    /// The allowed scopes for the client.
    /// </summary>
    IReadOnlyList<string> AllowedScopes { get; set; }
}
