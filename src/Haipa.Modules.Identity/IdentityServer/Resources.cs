using IdentityServer4.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Haipa.Modules.Identity.IdentityServer
{
    internal class Resources
    {
        private List<IdentityResource> identityResources;
        private List<ApiResource> apiResources;

        public Resources(List<IdentityResource> identityResources, List<ApiResource> apiResources)
        {
            this.identityResources = identityResources;
            this.apiResources = apiResources;
        }

        public static IEnumerable<IdentityResource> GetIdentityResources()
        {
            return new List<IdentityResource> {
            new IdentityResources.OpenId(),
            new IdentityResources.Profile(),
            new IdentityResources.Email(),
            new IdentityResource {
                Name = "role",
                UserClaims = new List<string> {"role"}
            },
        };
        }

        public static IEnumerable<ApiResource> GetApiResources()
        {
            return new List<ApiResource> {
            new ApiResource {
                Name = "haipa",
                DisplayName = "Haipa API",
                Description = "Haipa API Access",
                UserClaims = new List<string> {"role"},
                ApiSecrets = new List<Secret> {new Secret("password".Sha256())},
                Scopes = new List<Scope> {
                    new Scope("haipa.read"),
                    new Scope("haipa.write")
                }
            }
        };
        }
    }
}
