using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Eryph.Resources.Machines;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CatletStopMode
{
    /// <summary>
    /// This mode attempts to gracefully shut down the catlet.
    /// </summary>
    Shutdown = 0,

    /// <summary>
    /// This mode immediately stops the catlet comparable to pulling the power plug.
    /// </summary>
    Hard = 1,
    
    /// <summary>
    /// This mode terminates the Hyper-V worker process of the catlet. This
    /// mode circumvents the normal logic Hyper-V and should only be used
    /// when the catlet does not respond to commands in Hyper-V. This mode
    /// can cause inconsistencies in Hyper-V.
    /// </summary>
    Kill = 2,
}
