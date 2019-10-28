
using Haipa.Modules.Hosting;
using Haipa.Modules.Identity;

using Microsoft.AspNetCore;
using SimpleInjector;

namespace Haipa.Identity
{
    internal static class IdentityContainerExtensions
    {
        public static void Bootstrap(this Container container, string[] args)
        {
            container.HostModule<IdentityModule>();

            container.HostAspNetCore((path) => WebHost.CreateDefaultBuilder(args));

        }

    }
}
