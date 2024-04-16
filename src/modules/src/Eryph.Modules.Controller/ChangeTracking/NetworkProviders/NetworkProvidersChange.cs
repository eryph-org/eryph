using System.Collections.Generic;
using LanguageExt;

namespace Eryph.Modules.Controller.ChangeTracking.NetworkProviders;

internal record NetworkProvidersChange(Seq<string> ProviderNames);
