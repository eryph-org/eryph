using System;

namespace Eryph.Runtime.Zero.HttpSys;

public record SslOptions(
    Uri Url,
    int ValidDays,
    Guid ApplicationId);
