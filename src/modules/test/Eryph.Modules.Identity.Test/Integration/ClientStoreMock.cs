//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;
//using Dbosoft.IdentityServer;
//using Dbosoft.IdentityServer.Models;
//using Dbosoft.IdentityServer.Storage.Models;
//using Dbosoft.IdentityServer.Storage.Stores;

//namespace Eryph.Modules.Identity.Test.Integration;

//public class ClientStoreMock : IClientStore
//{
//    public Dictionary<string,Client> Clients { get; } = new();

//    public void AddSimpleClient(string clientName, IEnumerable<string> scopes)
//    {
//        Clients.Add(clientName, new Client
//        {
//            ClientId = clientName,
//            ClientSecrets = new List<Secret>(new[]
//            {
//                new Secret
//                {
//                    Type = IdentityServerConstants.SecretTypes.X509CertificateBase64,
//                    Value = TestClientData.CertificateString
//                }
//            }),
//            AllowedGrantTypes = GrantTypes.ClientCredentials,
//            AllowOfflineAccess = true,
//            AllowedScopes = scopes.ToArray(),
//            AllowRememberConsent = true,
//            RequireConsent = false
//        });
//    }

//    public Task<Client> FindClientByIdAsync(string clientId)
//    {
//        if(!Clients.TryGetValue(clientId, out var client))
//            return null;

//        return Task.FromResult(client);
//    }

//}