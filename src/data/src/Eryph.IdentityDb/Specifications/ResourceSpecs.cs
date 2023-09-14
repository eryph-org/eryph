using System;
using Ardalis.Specification;
using Eryph.IdentityDb.Entities;

namespace Eryph.IdentityDb.Specifications
{


    public static class ClientSpecs
    {

        public sealed class GetAll : Specification<ClientApplicationEntity>
        {
            public GetAll(Guid tenantId)
            {

                Query.Where(x => x.TenantId == tenantId);
                Query.OrderBy(x => x.Id);

            }
        }

        public sealed class GetByClientId : Specification<ClientApplicationEntity>, ISingleResultSpecification<ClientApplicationEntity>
        {
            public GetByClientId(string clientId, Guid tenantId)
            {
                Query.Where(x => x.TenantId == tenantId && x.ClientId == clientId);

            }

        }

    }




}