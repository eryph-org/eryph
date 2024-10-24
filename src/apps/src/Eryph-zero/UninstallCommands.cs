using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.Security.Cryptography;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.Runtime.Zero;

using static Logger<DriverCommandsRuntime>;
using static OvsDriverProvider<DriverCommandsRuntime>;

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

    private static Eff<Unit> RemoveCertificateAndKey(string keyName) =>
        from _1 in SuccessEff(unit)
        let keyStore = new WindowsCertificateKeyService()
        let certStore = new WindowsCertificateStoreService()
        from keyPair in Eff(() => Optional(keyStore.GetPersistedRsaKey(keyName)))
        from _2 in keyPair.Match(
            Some: kp =>
                from _1 in SuccessEff(unit)
                from publicKey in Eff(() => new PublicKey(kp))
                from _2 in Eff(() =>
                {
                    certStore.RemoveFromMyStore(publicKey);
                    return unit;
                })
                from _3 in Eff(() =>
                {
                    certStore.RemoveFromMyStore(publicKey);
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
}
