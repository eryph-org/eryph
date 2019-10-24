using IdentityServer4.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Text;

namespace Haipa.IdentityDb.Models
{
    public class ClientEntity
    {
        public string ClientData { get; set; }
        [Key]
        public Guid ClientId { get; set; }
        [NotMapped]
        public Client Client { get; set; }
        public string ConfigFile { get; set; }
        public bool ConfigFileExists
        {
            get
            {
                return (!String.IsNullOrEmpty(ConfigFile));
            }
        }
        public void AddDataToEntity()
        {
            ClientData = JsonConvert.SerializeObject(Client);
            ClientId = Guid.Parse(Client.ClientId);
        }
        public void SaveToFile()
        {
            ClientData = JsonConvert.SerializeObject(Client);
            ClientId = Guid.Parse(Client.ClientId);
            ConfigFile = Path.Combine(Haipa.IdentityDb.Config.GetConfigPath(), ClientId + ".json");
            System.IO.File.WriteAllText(ConfigFile, ClientData);
        }
        public void UpdateToFile()
        {
            ClientData = JsonConvert.SerializeObject(Client);
            ClientId = Guid.Parse(Client.ClientId);
            ConfigFile = Path.Combine(Haipa.IdentityDb.Config.GetConfigPath(), ClientId + ".json");
            if (File.Exists(ConfigFile))
            {
                File.Delete(ConfigFile);
            }
            System.IO.File.WriteAllText(ConfigFile, ClientData);
        }
        public void DeleteFile()
        {
            if (File.Exists(ConfigFile))
            {
                File.Delete(ConfigFile);
            }
        }
        public void MapDataFromEntity()
        {
            Client = JsonConvert.DeserializeObject<Client>(ClientData);
            ClientId = Guid.Parse(Client.ClientId);
        }
    }
}
