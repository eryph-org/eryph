using System;

namespace Eryph.Runtime.Zero.HttpSys;

public record SslOptions(
    string SubjectDnsName,
    int ValidDays,
    Guid ApplicationId,
    Uri Url);
