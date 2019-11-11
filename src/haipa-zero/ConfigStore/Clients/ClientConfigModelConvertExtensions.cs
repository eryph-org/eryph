using Haipa.Modules.Identity.Models;

namespace Haipa.Runtime.Zero.ConfigStore.Clients
{
    internal static class ClientConfigModelConvertExtensions
    {
        public static ClientConfigModel FromApiModel(this ClientEntityDTO apiModel)
        {
            return new ClientConfigModel
            {
                ClientId = apiModel.ClientId,
                AllowedScopes = apiModel.AllowedScopes,
                X509CertificateBase64 = apiModel.X509CertificateBase64,
                Description = apiModel.Description
            };
        }

        public static ClientEntityDTO ToApiModel(this ClientConfigModel configModel)
        {
            return new ClientEntityDTO
            {
                ClientId = configModel.ClientId,
                AllowedScopes = configModel.AllowedScopes,
                X509CertificateBase64 = configModel.X509CertificateBase64,
                Description = configModel.Description
            };
        }
    }
}