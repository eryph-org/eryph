using Haipa.IdentityDb.Extensions;
using Haipa.IdentityDb.Models;
using IdentityServer4.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Haipa.Modules.Identity.Seeder
{
    public class Config
    {       
        public static IEnumerable<ClientEntity> GetClients()
        {
            List<ClientEntity> Clients = new List<ClientEntity>();
            string FilePath = Haipa.IdentityDb.Config.GetConfigPath();
            DirectoryInfo D = new DirectoryInfo(FilePath);

            foreach (var currentConfigFile in D.GetFiles("*.json"))
            {
                string c = File.ReadAllText(currentConfigFile.FullName);
                if (!String.IsNullOrEmpty(c))
                {
                        ClientEntity o = new ClientEntity { ClientData = c, ConfigFile  = currentConfigFile.FullName };
                        o.MapDataFromEntity();
                        Clients.Add(o);
                }
            }
            return Clients;
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
     
    }
}

