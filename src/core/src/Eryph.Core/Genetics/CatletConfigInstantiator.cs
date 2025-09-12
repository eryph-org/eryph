using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Network;

using static LanguageExt.Prelude;

namespace Eryph.Core.Genetics;

public static class CatletConfigInstantiator
{
    /// <summary>
    /// This method sets some values which are specific to the deployed instance
    /// of a catlet (MAC addresses, storage location). The resulting <see cref="CatletConfig"/>
    /// can be used to deploy an instance of the catlet.
    /// </summary>
    /// <remarks>
    /// The <paramref name="location"/> must be a unique value as it is used to create
    /// a unique storage path for the catlet instance.
    /// </remarks>
    public static CatletConfig Instantiate(CatletConfig config, string location) =>
        InstantiateUpdate(config.CloneWith(c =>
        {
            c.Location = Optional(c.Location).Filter(notEmpty).IfNone(location);
        }));

    /// <summary>
    /// This method ensures that the <see cref="CatletConfig"/> for an update is
    /// fully instantiated. The user might have added additional drives or network adapters
    /// which do not have storage identifiers or MAC addresses assigned yet.
    /// </summary>
    public static CatletConfig InstantiateUpdate(CatletConfig config) =>
        config.CloneWith(c =>
        {
            c.ConfigType = CatletConfigType.Instance;
            c.Drives = c.Drives.ToSeq()
                .Map(d => ApplyStorageIdentifier(d, c.Location))
                .ToArray(); ;
            c.NetworkAdapters = c.NetworkAdapters.ToSeq()
                .Map(GenerateMacAddress)
                .ToArray();
        });

    private static CatletDriveConfig ApplyStorageIdentifier(
        CatletDriveConfig driveConfig,
        string storageIdentifier) =>
        driveConfig.CloneWith(d =>
        {
            d.Location = Optional(d.Location).Filter(notEmpty).IfNone(storageIdentifier);
        });

    private static CatletNetworkAdapterConfig GenerateMacAddress(
        CatletNetworkAdapterConfig adapterConfig) =>
        adapterConfig.CloneWith(a =>
        {
            a.MacAddress = Optional(a.MacAddress).Filter(notEmpty)
                .IfNone(() => MacAddressGenerator.Generate().Value);
        });
}
