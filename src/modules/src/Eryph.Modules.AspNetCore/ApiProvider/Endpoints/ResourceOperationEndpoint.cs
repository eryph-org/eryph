using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;

namespace Eryph.Modules.AspNetCore.ApiProvider.Endpoints;

public abstract class ResourceOperationEndpoint<TRequest,TResource> : OperationRequestEndpoint<TRequest, TResource> 
    where TResource : Resource 
    where TRequest : SingleEntityRequest
{
    protected ResourceOperationEndpoint(IOperationRequestHandler<TResource> operationHandler, 
        ISingleEntitySpecBuilder<SingleEntityRequest, TResource> specBuilder) : base(operationHandler, specBuilder)
    {

    }
}