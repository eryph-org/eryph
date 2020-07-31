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
                ClientId = apiModel.Id,
                ClientName = apiModel.Name,
                AllowedScopes = apiModel.AllowedScopes,
                X509CertificateBase64 = apiModel.Certificate,
                Description = apiModel.Description
            };
        }

        public static ClientApiModel ToApiModel(this ClientConfigModel configModel)
        {
            return new ClientApiModel
            {
                Id = configModel.ClientId,
                Name = configModel.ClientName,
                AllowedScopes = configModel.AllowedScopes,
                Certificate = configModel.X509CertificateBase64,
                Description = configModel.Description
            };
        }
    }
}