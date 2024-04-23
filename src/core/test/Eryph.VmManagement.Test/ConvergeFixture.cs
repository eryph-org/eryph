using Eryph.ConfigModel.Catlets;
using Eryph.Core.VmAgent;
using Eryph.Resources.Machines;
using Eryph.VmManagement.Converging;
using Eryph.VmManagement.Storage;

namespace Eryph.VmManagement.Test;

public class ConvergeFixture
{
    public ConvergeFixture()
    {
        Reset();
    }

    public void Reset()
    {
        var mapping = new FakeTypeMapping();
        Engine = new TestPowershellEngine(mapping);
        Config = new CatletConfig();
        StorageSettings = new VMStorageSettings();
        NetworkSettings = Array.Empty<MachineNetworkSettings>();
        HostInfo = new VMHostMachineData();
        VmHostAgentConfiguration = new VmHostAgentConfiguration()
        {
            Defaults = new VmHostAgentDefaultsConfiguration
            {
                Vms = "x:\\data",
                Volumes = "x:\\disks",
            },
        };
    }

    public VMStorageSettings StorageSettings { get; set; }


    public TestPowershellEngine Engine { get; private set; } = null!;
    public MachineNetworkSettings[] NetworkSettings { get; set; } = null!;
    public CatletConfig Config { get; set; } = null!;
    public CatletMetadata Metadata { get; set; } = null!;

    public VMHostMachineData HostInfo { get; set; } = null!;

    public VmHostAgentConfiguration VmHostAgentConfiguration { get; set; } = null!;


    public ConvergeContext Context =>
        new(VmHostAgentConfiguration, Engine, ReportProgressCallBack,
            Config, Metadata, StorageSettings, NetworkSettings, HostInfo);

    private static Task ReportProgressCallBack(string _) => Task.CompletedTask;

}