namespace Eryph.Modules.AspNetCore.ApiProvider.Model;

public interface IListFilteredByProjectRequest : IListRequest
{
    public string? ProjectId { get; }
}
