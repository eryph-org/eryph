namespace Eryph.Modules.AspNetCore.ApiProvider.Model;

public interface IListInProjectRequest : IListRequest
{
    public string ProjectId { get; }
}
