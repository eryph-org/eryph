using Haipa.IdentityDb.Models;
using IdentityServer4.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Haipa.IdentityDb.Extensions
{
   public static class ClientEntityExtensions
    {
        public static void UpdateToFile(this ClientEntity c)
        {
            c.ClientData = JsonConvert.SerializeObject(c.Client);
            c.ClientId = Guid.Parse(c.Client.ClientId);
            c.ConfigFile = Path.Combine(Haipa.IdentityDb.Config.GetConfigPath(), c.ClientId + ".json");
            if (File.Exists(c.ConfigFile))
            {
                File.Delete(c.ConfigFile);
            }
            System.IO.File.WriteAllText(c.ConfigFile, c.ClientData);
        }
        public static void AddDataToEntity(this ClientEntity c)
        {
            c.ClientData = JsonConvert.SerializeObject(c.Client);
            c.ClientId = Guid.Parse(c.Client.ClientId);
        }
        public static void SaveToFile(this ClientEntity c)
        {
            c.ClientData = JsonConvert.SerializeObject(c.Client);
            c.ClientId = Guid.Parse(c.Client.ClientId);
            c.ConfigFile = Path.Combine(Haipa.IdentityDb.Config.GetConfigPath(), c.ClientId + ".json");
            System.IO.File.WriteAllText(c.ConfigFile, c.ClientData);
        }
        public static void DeleteFile(this ClientEntity c)
        {
            if (File.Exists(c.ConfigFile))
            {
                File.Delete(c.ConfigFile);
            }
        }
        public static void MapDataFromEntity(this ClientEntity c)
        {
            c.Client = JsonConvert.DeserializeObject<Client>(c.ClientData);
            c.ClientId = Guid.Parse(c.Client.ClientId);
        }

        public static bool ConfigFileExists(this ClientEntity c)
        {          
                return (!String.IsNullOrEmpty(c.ConfigFile));
        }
    }
}
