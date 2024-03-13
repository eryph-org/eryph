using System;
using System.Net;
using System.Threading;
using Eryph.ConfigModel.Catlets;
using Eryph.StateDb.Model;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.Controller.Networks
{
    public interface ICatletIpManager
    {
        public EitherAsync<Error, IPAddress[]> ConfigurePortIps(
            Guid projectId, string environment, CatletNetworkPort port,
            CatletNetworkConfig[] networkConfigs, CancellationToken cancellationToken);

    }
}
