using System.Linq;
using Haipa.Configuration.Model;
using Haipa.Modules.Identity.Models;
using Haipa.Modules.Identity.Models.V1;

namespace Haipa.Runtime.Zero.Configuration.Clients
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
                Description = apiModel.Description
            };
        }

        public static Client ToApiModel(this ClientConfigModel configModel)
        {
            var client = new Client
            {
                Id = configModel.ClientId,
                Name = configModel.ClientName,
                AllowedScopes = configModel.AllowedScopes?.ToList(),
                Description = configModel.Description
            };

            var clientAsApiModel = (IClientApiModel) client;
            clientAsApiModel.Certificate = configModel.X509CertificateBase64;

            return client;
        }
    }
}