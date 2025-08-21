using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Eryph.Core;
using Eryph.Modules.Controller.Serializers;
using Eryph.Modules.HostAgent;
using Eryph.Modules.HostAgent.Configuration;
using Eryph.Modules.HostAgent.Networks;
using Eryph.Resources.Machines;
using Eryph.Runtime.Zero.Configuration;
using Eryph.Security.Cryptography;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Sys;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Sys.IO;
using static LanguageExt.Prelude;
using static LanguageExt.Seq;

namespace Eryph.Runtime.Zero;

using static Directory<DriverCommandsRuntime>;
using static File<DriverCommandsRuntime>;
using static Logger<DriverCommandsRuntime>;
using static OvsDriverProvider<DriverCommandsRuntime>;
using static VirtualMachineUtils<DriverCommandsRuntime>;

internal class UninstallCommands
{
    public static Aff<DriverCommandsRuntime, Unit> RemoveNetworking() =>
        from _1 in RemoveOverlaySwitch()
                   | @catch(e => logWarning<UninstallCommands>(
                       e, "The eryph overlay switch could not be removed. If necessary, remove it manually."))
        from _2 in RemoveNetNats()
                   | @catch(e => logWarning<UninstallCommands>(
                       e, "The NAT configurations could not be removed. If necessary, remove them manually."))
        from _3 in RemoveDrivers()
                   | @catch(e => logWarning<UninstallCommands>(
                       e, "The Hyper-V switch extension could not be removed. If necessary, remove it manually."))
        select unit;

    public static Aff<DriverCommandsRuntime, Unit> RemoveOverlaySwitch() =>
        from _1 in logInformation<UninstallCommands>("Removing eryph overlay switch...")
        from hostNetworkCommands in default(DriverCommandsRuntime).HostNetworkCommands
        from allSwitches in hostNetworkCommands.GetSwitches()
        let allOverlaySwitches = allSwitches.Filter(s => s.Name == EryphConstants.OverlaySwitchName)
        from allNetworkAdapters in allOverlaySwitches
            .Map(s => hostNetworkCommands.GetNetAdaptersBySwitch(s.Id))
            .SequenceSerial()
            .Map(l => l.Flatten())
        from _2 in hostNetworkCommands.DisconnectNetworkAdapters(allNetworkAdapters)
        from _3 in hostNetworkCommands.RemoveOverlaySwitch()
        select unit;

    public static Aff<DriverCommandsRuntime, Unit> RemoveNetNats() =>
        from _1 in logInformation<UninstallCommands>("Removing NAT configurations...")
        from hostNetworkCommands in default(DriverCommandsRuntime).HostNetworkCommands
        from netNats in hostNetworkCommands.GetNetNat()
        let eryphNetNats = netNats.Filter(n => n.Name.StartsWith("eryph_"))
        from _2 in eryphNetNats
            .Map(n => hostNetworkCommands.RemoveNetNat(n.Name))
            .SequenceSerial()
        select unit;

    public static Aff<DriverCommandsRuntime, Unit> RemoveDrivers() =>
        from _1 in logInformation<UninstallCommands>("Removing Hyper-V switch extension...")
        from _2 in uninstallDriver()
        from _3 in removeAllDriverPackages()
        select unit;

    public static Eff<Unit> RemoveCertificatesAndKeys() =>
        from _ in Seq(
                "eryph-zero-tls-key",
                "eryph-identity-token-encryption-key",
                "eryph-identity-token-signing-key")
            .Map(RemoveCertificateAndKey)
            .Sequence()
        select unit;

    public static Aff<DriverCommandsRuntime, Unit> RemoveCatletsAndDisk() =>
        from _1 in logInformation<UninstallCommands>("Removing catlets and disks...")
        from _2 in RemoveCatlets()
                   | @catch(e => logWarning<UninstallCommands>(
                       e, "The catlets could not be removed. If necessary, remove them manually."))
        from _3 in RemoveStores()
                   | @catch(e => logWarning<UninstallCommands>(
                       e, "The catlet disks could not be removed. If necessary, remove them manually."))
        select unit;

    private static Eff<Unit> RemoveCertificateAndKey(string keyName) =>
        from _1 in SuccessEff(unit)
        let keyStore = new WindowsCertificateKeyService()
        let certStore = new WindowsCertificateStoreService()
        from keyPair in Eff(() => Optional(keyStore.GetPersistedRsaKey(keyName)))
        from _2 in keyPair.Match(
            Some: kp =>
                from publicKey in Eff(() => new PublicKey(kp))
                from _1 in Eff(() =>
                {
                    certStore.RemoveFromMyStore(publicKey);
                    return unit;
                })
                from _2 in Eff(() =>
                {
                    certStore.RemoveFromRootStore(publicKey);
                    return unit;
                })
                select unit,
            None: () => SuccessEff(unit))
        from _3 in Eff(() =>
        {
            keyStore.DeletePersistedKey(keyName);
            return unit;
        })
        select unit;

    private static Aff<DriverCommandsRuntime, Unit> RemoveCatlets() =>
        from catletMetadataPaths in enumerateFiles(ZeroConfig.GetMetadataConfigPath(), "*.json")
            .IfFail(Seq<string>())
        from vmIds in catletMetadataPaths.Map(GetOptionalVmId).SequenceSerial()
        let validVmIds = vmIds.Somes()
        from _ in validVmIds.Map(TryStopAndRemoveVm).SequenceSerial()
        select unit;

    private static Aff<DriverCommandsRuntime, Option<Guid>> GetOptionalVmId(string path) =>
        GetVmId(path).Map(Some)
        | @catch(e => from _ in logWarning<UninstallCommands>(e, "Could not get VM ID from metadata '{Path}'.", path)
                      select Option<Guid>.None);

    private static Aff<DriverCommandsRuntime, Guid> GetVmId(
        string path) =>
        from metadataJson in readAllText(path)
        from metadataInfo in Eff(() => CatletMetadataJsonSerializer.DeserializeInfo(metadataJson))
        select metadataInfo.VmId;

    private static Aff<DriverCommandsRuntime, Unit> TryStopAndRemoveVm(Guid vmId) =>
        StopAndRemoveVm(vmId) | @catch(e => logWarning<UninstallCommands>(
            e, "Could not remove Hyper-V VM {VmId}. If necessary, remove it manually.", vmId));

    private static Aff<DriverCommandsRuntime, Unit> StopAndRemoveVm(Guid vmId) =>
        from _1 in logInformation<UninstallCommands>("Removing Hyper-V VM {VmId}...", vmId)
        from vmInfo in getOptionalVmInfo(vmId)
        // We only remove the VM here. Any leftover files will be removed later
        // when we remove the stores.
        from _2 in vmInfo.Map(StopAndRemoveVm).Sequence()
        select unit;

    private static Aff<DriverCommandsRuntime, Unit> StopAndRemoveVm(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        // We only remove the VM here. Any leftover files will be removed later
        // when we remove the stores.
        timeout(TimeSpan.FromSeconds(15), stopVm(vmInfo)).Bind(_ => removeVm(vmInfo))
        // We try to remove the VM whether it was stopped successfully or not.
        // Maybe, we are lucky and can remove the VM even after the stop command failed.
        | @catch(stopError => removeVm(vmInfo).MapFail(removeError => Error.Many(stopError, removeError)));

    private static Aff<DriverCommandsRuntime, Unit> RemoveStores() =>
        from hostSettings in HostSettingsProvider<DriverCommandsRuntime>.getHostSettings()
        from vmHostAgentConfig in VmHostAgentConfiguration<DriverCommandsRuntime>.readConfig(
            Path.Combine(ZeroConfig.GetVmHostAgentConfigPath(), "agentsettings.yml"),
            hostSettings)
        let paths = append(
            vmHostAgentConfig.Environments.ToSeq()
                .Bind(e => e.Datastores.ToSeq())
                .Map(ds => ds.Path),
            vmHostAgentConfig.Environments.ToSeq()
                .Bind(e => Seq(e.Defaults.Vms, e.Defaults.Volumes)),
            vmHostAgentConfig.Datastores.ToSeq()
                .Map(ds => ds.Path),
            Seq1(vmHostAgentConfig.Defaults.Vms),
            Seq1(vmHostAgentConfig.Defaults.Volumes))
        from _ in paths.Map(RemoveStore).SequenceSerial()
        select unit;

    private static Aff<DriverCommandsRuntime, Unit> RemoveStore(string path) =>
        delete(path, recursive: true) | @catch(e => logWarning<UninstallCommands>(
            e, "The store '{Path}' could not be removed. If necessary, remove it manually.", path));
}
