using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.ComputeApi.Model.V1;
using Microsoft.AspNetCore.Mvc;
using VirtualCatlet = Eryph.StateDb.Model.VirtualCatlet;

namespace Eryph.Modules.ComputeApi.Handlers
{
    internal class GetVirtualCatletConfigurationHandler : IGetRequestHandler<VirtualCatlet, 
        VirtualCatletConfiguration>
    {
        private readonly IReadRepositoryBase<VirtualCatlet> _repository;

        public GetVirtualCatletConfigurationHandler(IReadRepositoryBase<VirtualCatlet> repository)
        {
            _repository = repository;
        }

        public async Task<ActionResult<VirtualCatletConfiguration>> HandleGetRequest(Func<ISingleResultSpecification<VirtualCatlet>> specificationFunc, CancellationToken cancellationToken)
        {
            var vCatletSpec = specificationFunc();
            var vCatlet = await _repository.GetBySpecAsync(vCatletSpec, cancellationToken);


            throw new NotImplementedException();
        }
    }
}
