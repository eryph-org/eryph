using System.Net;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.Core;

public static class Prelude
{
    public static Option<IPNetwork2> parseIPNetwork2(
        string network) =>
        IPNetwork2.TryParse(network, out var ipNetwork) ? ipNetwork : None;

    public static Option<IPNetwork2> parseIPNetwork2(
        string ipAddress,
        string netmask) =>
        IPNetwork2.TryParse(ipAddress, netmask, out var ipNetwork) ? ipNetwork : None;
}
