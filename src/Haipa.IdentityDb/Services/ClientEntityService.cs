using Haipa.IdentityDb.Dtos;
using Haipa.IdentityDb.Extensions;
using Haipa.IdentityDb.Models;
using Haipa.IdentityDb.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Haipa.IdentityDb;
using IdentityServer4.Models;
using Microsoft.EntityFrameworkCore;
using IdentityServer4;

namespace Haipa.IdentityDb.Services
{
    public class ClientEntityService : IClientEntityService
    {
        private readonly ConfigurationStoreContext _db;

        public ClientEntityService(ConfigurationStoreContext context)
        {
            _db = context;
        }
        public IQueryable<ClientEntityDTO> GetClient()
        {
             var clients = _db.Clients.Select(c =>
            new ClientEntityDTO()
            {
                ClientId = c.ClientId,
                Description = c.Client.Description,
                X509CertificateBase64 = c.Client.ClientSecrets.FirstOrDefault().Value
            });
  
            return clients;
        }
        public virtual async Task<int> DeleteClient(Guid clientId)
        {
            var client = _db.Clients.Where(i => i.ClientId == clientId);
            if (client.Count() > 1)
            {
                return Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest;
            }
            if (client.Count() == 0)
            {
                return Microsoft.AspNetCore.Http.StatusCodes.Status404NotFound;
            }
            var c = client.First();
            if (c.ConfigFileExists())
            {
                c.DeleteFile();
            }
            _db.Clients.Remove(c);
            await _db.SaveChangesAsync();
            return Microsoft.AspNetCore.Http.StatusCodes.Status200OK;
        }
        public virtual async Task<int> PutClient(ClientEntityDTO client)
        {
            string clientId = client.ClientId.ToString();
            var findResult = _db.Clients.Where(a => a.ClientId == client.ClientId);
            if (findResult.Count() == 0)
            {
                return Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest;
            }
            if (findResult.Count() > 1)
            {
                return Microsoft.AspNetCore.Http.StatusCodes.Status409Conflict;

            }
            if (client == null)
            {
                return Microsoft.AspNetCore.Http.StatusCodes.Status409Conflict;
            }
            if (client.ClientId == null || client.AllowedScopes == null || client.X509CertificateBase64 == null)
            {
                return Microsoft.AspNetCore.Http.StatusCodes.Status409Conflict;
            }
            var c = findResult.First();
            var tempConfigFile = c.ConfigFile;

            var clientEntity = new ClientEntity
            {
                Client = new IdentityServer4.Models.Client
                {
                    ClientName = clientId,
                    ClientId = clientId,
                    ClientSecrets = new List<Secret>
                     {
                          new Secret
                            {
                                Type = IdentityServerConstants.SecretTypes.X509CertificateBase64,
                                Value = client.X509CertificateBase64
                            }
                     },
                    Description = client.Description,
                    AllowedGrantTypes = GrantTypes.ClientCredentials,
                    AllowOfflineAccess = true,
                    AllowedScopes = client.AllowedScopes,
                    AllowRememberConsent = true,
                    RequireConsent = false,
                },
                ConfigFile = tempConfigFile
            };
            clientEntity.AddDataToEntity();
            _db.Entry(c).CurrentValues.SetValues(clientEntity);
            c.MapDataFromEntity();
            await _db.SaveChangesAsync();
            if (c.ConfigFileExists())
            {
                c.UpdateToFile();
            }
            return Microsoft.AspNetCore.Http.StatusCodes.Status200OK;
        }
        public virtual async Task<int> PostClient(ClientEntityDTO client)
        {
            if (client == null)
            {
                return Microsoft.AspNetCore.Http.StatusCodes.Status409Conflict;
            }
            if (client.AllowedScopes == null || client.X509CertificateBase64 == null)
            {
                return Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest;
            }

            //Guid newGuid = Guid.NewGuid();
            string clientId = client.ClientId.ToString(); // newGuid.ToString();

            var clientEntity = new ClientEntity
            {
                Client = new IdentityServer4.Models.Client
                {
                    ClientName = clientId,
                    ClientId = clientId,
                    ClientSecrets = new List<Secret>
                         {
                              new Secret
                            {
                                Type = IdentityServerConstants.SecretTypes.X509CertificateBase64,
                                Value = client.X509CertificateBase64
                            }
                         },
                    Description = client.Description,
                    AllowedGrantTypes = GrantTypes.ClientCredentials,
                    AllowOfflineAccess = true,
                    AllowedScopes = client.AllowedScopes,
                    AllowRememberConsent = true,
                    RequireConsent = false,
                },
            };

            clientEntity.AddDataToEntity();
            clientEntity.SaveToFile();
            _db.Clients.Add(clientEntity);
            await _db.SaveChangesAsync();
            return Microsoft.AspNetCore.Http.StatusCodes.Status200OK;
        }

    }
}
