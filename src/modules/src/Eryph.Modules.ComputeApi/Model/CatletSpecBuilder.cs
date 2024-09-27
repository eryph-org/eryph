using Ardalis.Specification;
using Eryph.Modules.AspNetCore;
using Eryph.StateDb.Model;
using Microsoft.AspNetCore.Http;

namespace Eryph.Modules.ComputeApi.Model
{
    public class CatletSpecBuilder : ResourceSpecBuilder<Catlet>
    {
        public CatletSpecBuilder(IUserRightsProvider userRightsProvider) : base(userRightsProvider)
        {
        }

        protected override void CustomizeQuery(ISpecificationBuilder<Catlet> query)
        {
            query.Include(x => x.ReportedNetworks);
        }
    }
}