using System;
using System.Collections.Generic;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Microsoft.AspNetCore.Http;

namespace Eryph.Modules.ComputeApi.Model
{
    public class VirtualCatletSpecBuilder : ResourceSpecBuilder<VirtualCatlet>
    {
        public VirtualCatletSpecBuilder(IUserRightsProvider userRightsProvider) : base(userRightsProvider)
        {
        }
    }
}