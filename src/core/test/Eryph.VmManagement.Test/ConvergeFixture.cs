using Dbosoft.OVN.Windows;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
using Eryph.Core.VmAgent;
using Eryph.Resources.Machines;
using Eryph.VmManagement.Converging;
using Eryph.VmManagement.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Eryph.VmManagement.Test;

public class ConvergeFixture
{
    public ConvergeFixture()
    {
        var mapping = new FakeTypeMapping();
        Engine = new TestPowershellEngine(mapping);
        PortManager = Mock.Of<IHyperVOvsPortManager>();
        Config = new CatletConfig();
        StorageSettings = new VMStorageSettings();
        NetworkSettings = [];
        HostInfo = new VMHostMachineData();
        VmHostAgentConfiguration = new VmHostAgentConfiguration()
        {
            Defaults = new VmHostAgentDefaultsConfiguration
            {
                Vms = "x:\\data",
                Volumes = "x:\\disks",
            },
        };
        Metadata = new CatletMetadata();
        ResolvedGenes = [];
    }

    public VMStorageSettings StorageSettings { get; set; }

    public TestPowershellEngine Engine { get;  }

    public IHyperVOvsPortManager PortManager { get; set; }

    public MachineNetworkSettings[] NetworkSettings { get; set; }
    
    public CatletConfig Config { get; set; }
    
    public CatletMetadata Metadata { get; set; }

    public VMHostMachineData HostInfo { get; set; }

    public VmHostAgentConfiguration VmHostAgentConfiguration { get; set; }

    public IReadOnlyList<UniqueGeneIdentifier> ResolvedGenes { get; set; }

    public ConvergeContext Context =>
        new(VmHostAgentConfiguration, Engine, PortManager, ReportProgressCallBack,
            Config, Metadata, StorageSettings, NetworkSettings, HostInfo,
            ResolvedGenes.ToSeq(), NullLoggerFactory.Instance);

    private static Task ReportProgressCallBack(string _) => Task.CompletedTask;

}