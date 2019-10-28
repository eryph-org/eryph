using Haipa.IdentityDb.Models;
using IdentityServer4.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Haipa.IdentityDb.Extensions
{
    public static class ApiResourceEntityExtensions
    {
        public static void AddDataToEntity(this ApiResourceEntity a)
        {
            a.ApiResourceData = JsonConvert.SerializeObject(a.ApiResource);
            a.ApiResourceName = a.ApiResource.Name;
        }

        public static void MapDataFromEntity(this ApiResourceEntity a)
        {
            a.ApiResource = JsonConvert.DeserializeObject<ApiResource>(a.ApiResourceData);
            a.ApiResourceName = a.ApiResource.Name;
        }
    }
}
