using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Eryph.IdentityDb;
using Eryph.IdentityDb.Entities;
using Eryph.IdentityDb.Specifications;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;

namespace Eryph.Modules.Identity.Services
{
    public class ClientService : BaseApplicationService<ClientApplicationEntity, ClientApplicationDescriptor>, IClientService
    {
        public ClientService(IOpenIddictApplicationManager applicationManager, IIdentityDbRepository<ClientApplicationEntity> repository) : base(applicationManager, repository)
        {
        }

        protected override async ValueTask PopulateApplicationFromDescriptor(ClientApplicationEntity application, ClientApplicationDescriptor descriptor,
            CancellationToken cancellationToken)
        {
            // Register the client's public key as a JSON Web Key Set so OpenIddict can natively
            // validate incoming client assertions (private_key_jwt). The base64 X.509 certificate
            // remains the stored source of truth and is preserved across updates (see Get/Update).
            descriptor.JsonWebKeySet = CreateJsonWebKeySet(descriptor.Certificate);

            await base.PopulateApplicationFromDescriptor(application, descriptor, cancellationToken);
            application.Certificate = descriptor.Certificate;

        }

        protected override async ValueTask PopulateDescriptorFromApplication(ClientApplicationDescriptor descriptor, ClientApplicationEntity application,
            CancellationToken cancellationToken)
        {
            await base.PopulateDescriptorFromApplication(descriptor, application, cancellationToken);
            descriptor.Certificate = application.Certificate;
        }


        protected override void InitializeDescriptor(ClientApplicationDescriptor descriptor)
        {
            descriptor.ClientType = OpenIddictConstants.ClientTypes.Confidential;

            //// as openiddict does automatically assumes that a client key is required for confidential clients
            //// we have to set a secure random string as client secret to avoid client to be filtered out
            descriptor.ClientSecret ??= Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

            descriptor.Permissions.Clear();
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);

        }

        protected override ISingleResultSpecification<ClientApplicationEntity> GetSingleEntitySpec(string clientId, Guid tenantId)
        {
            return new ClientSpecs.GetByClientId(clientId, tenantId);
        }

        protected override ISpecification<ClientApplicationEntity> GetListSpec(Guid tenantId)
        {
            return new ClientSpecs.GetAll(tenantId);
        }

        private static JsonWebKeySet? CreateJsonWebKeySet(string? certificateBase64)
        {
            if (string.IsNullOrWhiteSpace(certificateBase64))
                return null;

            X509Certificate2 certificate;
            try
            {
                certificate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(certificateBase64));
            }
            catch (Exception ex) when (ex is FormatException or CryptographicException)
            {
                throw new InvalidOperationException(
                    "The client certificate must be a base64-encoded X.509 certificate.", ex);
            }

            using (certificate)
            {
                var key = JsonWebKeyConverter.ConvertFromX509SecurityKey(
                    new X509SecurityKey(certificate), representAsRsaKey: true);

                var keySet = new JsonWebKeySet();
                keySet.Keys.Add(key);
                return keySet;
            }
        }
    }
}
