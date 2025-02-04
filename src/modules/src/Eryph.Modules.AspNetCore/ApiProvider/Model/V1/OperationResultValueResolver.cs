using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AutoMapper;
using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel.Json;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.StateDb.Model;
using SimpleInjector;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model.V1;

public class OperationResultValueResolver(
    Container container)
    : IValueResolver<OperationModel, Operation, OperationResult?>
{
    public OperationResult? Resolve(
        OperationModel source,
        Operation destination,
        OperationResult? destMember,
        ResolutionContext context)
    {
        if (source.ResultData is null || source.ResultType is null)
            return null;

        var workflowOptions = container.GetInstance<WorkflowOptions>();
        var type = Type.GetType(source.ResultType);
        if (type is null)
            throw new ArgumentException($"The result type '{source.ResultType}' was not found", nameof(source));

        var result = JsonSerializer.Deserialize(source.ResultData, type, workflowOptions.JsonSerializerOptions);
        if (result is ExpandNewCatletConfigCommandResponse expandResponse)
        {
            var configJson = CatletConfigJsonSerializer.SerializeToElement(expandResponse.Config);
            return new CatletConfigOperationResult
            {
                Configuration = configJson
            };
        }

        throw new ArgumentException($"The result type '{source.ResultType}' is not supported", nameof(source));
    }
}
