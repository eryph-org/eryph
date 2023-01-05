using System.Linq;
using Eryph.Configuration.Model;
using Eryph.Modules.Identity.Models;
using Eryph.Modules.Identity.Models.V1;

namespace Eryph.Runtime.Zero.Configuration.Clients
{
    internal static class ClientConfigModelConvertExtensions
    {
        public static ClientConfigModel FromApiModel<TModel>(this TModel apiModel) where TModel : IClientApiModel
        {
            return new ClientConfigModel
            {
                ClientId = apiModel.Id,
                ClientName = apiModel.Name,
                AllowedScopes = apiModel.AllowedScopes?.ToArray(),
                X509CertificateBase64 = apiModel.Certificate,
                Description = apiModel.Description,
                Roles = apiModel.Roles.ToArray(),
                TenantId = apiModel.Tenant
            };
        }

        public static Client ToApiModel(this ClientConfigModel configModel)
        {
            var client = new Client
            {
                Id = configModel.ClientId,
                Name = configModel.ClientName,
                AllowedScopes = configModel.AllowedScopes?.ToList(),
                Description = configModel.Description,
                Tenant = configModel.TenantId,
                Roles = configModel.Roles.ToList()
            };

            var clientAsApiModel = (IClientApiModel) client;
            clientAsApiModel.Certificate = configModel.X509CertificateBase64;

            return client;
        }
    }
}