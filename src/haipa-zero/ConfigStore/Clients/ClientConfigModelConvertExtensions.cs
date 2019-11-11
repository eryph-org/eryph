using Haipa.Modules.Identity.Models;
using Haipa.Modules.Identity.Models.V1;

namespace Haipa.Runtime.Zero.ConfigStore.Clients
{
    internal static class ClientConfigModelConvertExtensions
    {
        public static ClientConfigModel FromApiModel<TModel>(this TModel apiModel) where TModel: IClientApiModel
        {
            return new ClientConfigModel
            {
                ClientId = apiModel.ClientId,
                AllowedScopes = apiModel.AllowedScopes,
                X509CertificateBase64 = apiModel.X509CertificateBase64,
                Description = apiModel.Description
            };
        }

        public static ClientApiModel ToApiModel(this ClientConfigModel configModel)
        {
            return new ClientApiModel
            {
                ClientId = configModel.ClientId,
                AllowedScopes = configModel.AllowedScopes,
                X509CertificateBase64 = configModel.X509CertificateBase64,
                Description = configModel.Description
            };
        }
    }
}