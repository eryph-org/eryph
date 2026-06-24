using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Swagger;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Eryph.Modules.AspNetCore.Tests.ApiProvider.Swagger;

public class ListResponseOperationFilterTests
{
    private static OperationFilterContext ContextFor(string methodName) =>
        new(
            new ApiDescription(),
            null!,
            new SchemaRepository(),
            typeof(SampleActions).GetMethod(methodName)!);

    [Fact]
    public void Apply_WithMultipleResponsesNoneList_DoesNotThrowAndIsNotPageable()
    {
        // Regression: GetCustomAttribute (singular) threw AmbiguousMatchException for actions with
        // several [SwaggerResponse] (e.g. the identity component-enrollment endpoint), breaking the
        // whole swagger document.
        var operation = new OpenApiOperation();

        var act = () => new ListResponseOperationFilter()
            .Apply(operation, ContextFor(nameof(SampleActions.MultipleResponses)));

        act.Should().NotThrow();
        operation.Extensions.Should().NotContainKey("x-ms-pageable");
    }

    [Fact]
    public void Apply_WithListResponseAmongMultiple_AddsPageableExtension()
    {
        var operation = new OpenApiOperation();

        new ListResponseOperationFilter()
            .Apply(operation, ContextFor(nameof(SampleActions.PagedWithErrorResponses)));

        operation.Extensions.Should().ContainKey("x-ms-pageable");
    }

    private class SampleActions
    {
        [SwaggerResponse(200, "Success", typeof(string))]
        [SwaggerResponse(400, "Bad request")]
        [SwaggerResponse(401, "Unauthorized")]
        public void MultipleResponses()
        {
        }

        [SwaggerResponse(200, "Success", typeof(ListResponse<string>))]
        [SwaggerResponse(400, "Bad request")]
        public void PagedWithErrorResponses()
        {
        }
    }
}
