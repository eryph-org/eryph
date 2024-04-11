using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.ZeroState;

public class ZeroStateSeederException : Exception
{
    public ZeroStateSeederException(string message)
        : base(message)
    {
    }

    public ZeroStateSeederException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
