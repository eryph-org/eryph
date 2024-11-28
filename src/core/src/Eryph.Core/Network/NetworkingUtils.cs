using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.Core.Network;

public static class NetworkingUtils
{
    public static Option<IPNetwork2> parseIPNetwork(string value) =>
        IPNetwork2.TryParse(value, out var ip) ? Some(ip) : None;
}
