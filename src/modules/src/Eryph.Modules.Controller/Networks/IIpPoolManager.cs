﻿using System;
using System.Threading;
using Eryph.StateDb.Model;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.Controller.Networks;

public interface IIpPoolManager
{
    EitherAsync<Error, IpPoolAssignment> AcquireIp(
        Guid subnetId,
        string poolName,
        CancellationToken cancellationToken = default);

}