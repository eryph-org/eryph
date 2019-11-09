using Haipa.IdentityDb.Models;
using IdentityServer4.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Haipa.IdentityDb.Extensions
{
    public static class IdentityResourceEntityExtensions
    {
         public static void AddDataToEntity(this IdentityResourceEntity i)
        {
            i.IdentityResourceData = JsonConvert.SerializeObject(i.IdentityResource);
            i.IdentityResourceName = i.IdentityResource.Name;
        }

        public static void MapDataFromEntity(this IdentityResourceEntity i)
        {
            i.IdentityResource = JsonConvert.DeserializeObject<IdentityResource>(i.IdentityResourceData);
            i.IdentityResourceName = i.IdentityResource.Name;
        }
    }
}
