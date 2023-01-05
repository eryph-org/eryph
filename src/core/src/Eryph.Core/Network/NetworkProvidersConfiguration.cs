namespace Eryph.Core.Network;

public class NetworkProvidersConfiguration
{
    public NetworkProvider[] NetworkProviders { get; set; }

    public string[] EnabledBridges { get; set; }


    public const string DefaultConfig = @"
network_providers:
- name: default
  type: nat_overlay
  bridge_name: br-nat
  subnets: 
  - name: default
    network: 10.249.248.0/22
    gateway: 10.249.248.1
    ip_pools:
    - name: default
      first_ip: 10.249.248.10
      last_ip: 10.249.251.241
";
}