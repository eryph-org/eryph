﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers;

public interface IEntityOperationRequestHandler<TModel> where TModel: class
{
    Task<ActionResult<Operation>> HandleOperationRequest(
        Func<ISingleResultSpecification<TModel>?> specificationFunc, 
        Func<TModel, object> createOperationFunc, 
        CancellationToken cancellationToken);
}
