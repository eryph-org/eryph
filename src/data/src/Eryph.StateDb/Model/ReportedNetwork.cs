using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eryph.StateDb.Model;

public class ReportedNetwork
{
    public Guid Id { get; set; }

    public Guid CatletId { get; set; }

    public Catlet Catlet { get; set; } = null!;

    public IList<string> IpV4Addresses { get; set; } = [];

    public IList<string> IpV6Addresses { get; set; } = [];

    // ReSharper disable once InconsistentNaming
    public string? IPv4DefaultGateway { get; set; }

    // ReSharper disable once InconsistentNaming
    public string? IPv6DefaultGateway { get; set; }

    public IList<string> DnsServerAddresses { get; set; } = [];

    public IList<string> IpV4Subnets { get; set; } = [];

    public IList<string> IpV6Subnets { get; set; } = [];
}