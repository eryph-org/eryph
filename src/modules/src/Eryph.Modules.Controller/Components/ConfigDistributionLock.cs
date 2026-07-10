using System;
using Eryph.Messages.Components;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// The distributed-lock resource name that serializes all work on one configuration domain and scope.
/// Both authoring a new version (<see cref="AuthoredConfigStore"/>) and materializing the distributed
/// record (<see cref="ConfigDistributionService"/>) take THIS SAME lock, so a config-refresh that races
/// the still-open authoring unit of work blocks until the authoring commit is visible — closing the
/// send-before-commit window where a refresh would otherwise read and distribute the previous value and
/// then never re-trigger.
/// </summary>
internal static class ConfigDistributionLock
{
    // The scope is percent-escaped so a selector cannot introduce a character that is invalid in the
    // file-based lock name (notably ':' on Windows).
    public static string ForDomainScope(ConfigDomain domain, string scope) =>
        $"config-domain-{domain}-{Uri.EscapeDataString(scope)}";
}
