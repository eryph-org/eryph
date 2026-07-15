using System;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.Controller;

public interface IStorageManagementAgentLocator
{
    /// <summary>
    /// The host agent which manages a datastore in a site, or an error when no agent in that site is
    /// registered.
    /// </summary>
    /// <remarks>
    /// Takes the site rather than the environment on purpose. For an existing resource the caller
    /// passes the site pinned on it; only a caller creating a resource resolves the site from an
    /// environment. Resolving it here would re-derive the location of resources that already exist,
    /// which would move them whenever the environment configuration is re-authored.
    /// </remarks>
    Either<Error, string> FindAgentForDataStore(string dataStore, Guid siteId);

    string FindAgentForGenePool();
}
