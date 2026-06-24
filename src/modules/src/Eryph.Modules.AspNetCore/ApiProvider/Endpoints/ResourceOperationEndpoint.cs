using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;

namespace Eryph.Modules.AspNetCore.ApiProvider.Endpoints;

public abstract class ResourceOperationEndpoint<TRequest, TResource>(
    IEntityOperationRequestHandler<TResource> operationHandler,
    ISingleEntitySpecBuilder<SingleEntityRequest, TResource> specBuilder)
    : OperationRequestEndpoint<TRequest, TResource>(operationHandler, specBuilder)
    where TResource : Resource
    where TRequest : SingleEntityRequest;
