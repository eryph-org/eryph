namespace Eryph.Modules.AspNetCore.ApiProvider.Model.V1;

public class ProjectMemberRole
{
    public required string Id { get; set; }

    public required Project Project { get; set; }

    public required string MemberId { get; set; }

    public required string RoleId { get; set; }

    public required string RoleName { get; set; }
}
