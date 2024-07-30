using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Core;

public interface IApplicationInfoProvider
{
    /// <summary>
    /// The name of the application, e.g. eryph-zero.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The SemVer version of the application.
    /// </summary>
    public string ProductVersion { get; }

    /// <summary>
    /// The application ID which can be used with AutoRest
    /// clients. It is limited to 24 characters.
    /// </summary>
    public string ApplicationId { get; }
}
