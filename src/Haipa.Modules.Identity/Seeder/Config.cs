using Haipa.IdentityDb.Models;
using IdentityServer4.Models;
using System.Collections.Generic;

namespace Haipa.Modules.Identity.Demo
{
    public class Config
    {
        public static IEnumerable<ClientEntity> GetClients()
        {
            List<ClientEntity> clients = new List<ClientEntity>();
            foreach (var client in GetClientsInternal())
            {
                var clientEntity = new ClientEntity
                {
                    Client = client
                };
                clientEntity.AddDataToEntity();
                clients.Add(clientEntity);
            }

            return clients;
        }

        public static IEnumerable<IdentityResourceEntity> GetIdentityResources()
        {
            List<IdentityResourceEntity> identityResources = new List<IdentityResourceEntity>();
            foreach (var identityResource in GetIdentityResourcesInternal())
            {
                var identityResourceEntity = new IdentityResourceEntity
                {
                    IdentityResource = identityResource
                };
                identityResourceEntity.AddDataToEntity();
                identityResources.Add(identityResourceEntity);
            }

            return identityResources;
        }
        public static IEnumerable<ApiResourceEntity> GetApiResources()
        {
            List<ApiResourceEntity> apiResources = new List<ApiResourceEntity>();
            foreach (var apiResource in GetApiResourcesInternal())
            {
                var apiResourceEntity = new ApiResourceEntity
                {
                    ApiResource = apiResource
                };
                apiResourceEntity.AddDataToEntity();
                apiResources.Add(apiResourceEntity);
            }

            return apiResources;
        }
        private static IEnumerable<IdentityResource> GetIdentityResourcesInternal()
        {
            return new List<IdentityResource>
            {
                new IdentityResources.OpenId(),
                new IdentityResources.Profile(),
                new IdentityResources.Email(),
                new IdentityResource("testscope",new []{ "role1", "role2", "role3"} ),
            };
        }

        private static IEnumerable<ApiResource> GetApiResourcesInternal()
        {
            return new List<ApiResource>
            {
                    new ApiResource("compute_api")
            };
        }

        private static IEnumerable<Client> GetClientsInternal()
        {
            return new List<Client>
            {
                                new Client
                {
                    ClientName = "test3",
                    ClientId = "test3",
                                        ClientSecrets = new List<Secret>
                         {
                              new Secret("test3".Sha256())
                         },

                    AccessTokenType = AccessTokenType.Reference,
                    AccessTokenLifetime = 360,
                    IdentityTokenLifetime = 300,
                    AllowedGrantTypes = GrantTypes.ClientCredentials,
                    AllowedScopes = new List<string>
                    {
                        "compute_api"
                    }
                },
            };
        }
    }
}

