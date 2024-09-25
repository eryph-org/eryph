using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Security.Cryptography;

public static class Oids
{
    public static class EnhancedKeyUsage
    {
        public static readonly string ClientAuthentication = "1.3.6.1.5.5.7.3.2";
        public static readonly string ServerAuthentication = "1.3.6.1.5.5.7.3.1";
    }
}
