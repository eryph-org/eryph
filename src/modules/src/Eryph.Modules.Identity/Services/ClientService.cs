using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Eryph.IdentityDb;
using Eryph.IdentityDb.Entities;
using Eryph.IdentityDb.Specifications;
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
            descriptor.Type = OpenIddictConstants.ClientTypes.Confidential;

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
    }
}