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
    }
}