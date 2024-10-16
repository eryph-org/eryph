using System.Collections.Generic;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model;

public class ListResponse<T>
{
    public required IReadOnlyList<T> Value { get; set; }
}
