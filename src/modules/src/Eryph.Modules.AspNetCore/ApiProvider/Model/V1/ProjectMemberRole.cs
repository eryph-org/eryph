using System.ComponentModel.DataAnnotations;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model.V1;

public class ProjectMemberRole
{
    [Key] public string Id { get; set; }
    public string ProjectId { get; set; }
    public string ProjectName { get; set; }

    public string MemberId { get; set; }
    public string RoleId { get; set; }
    public string RoleName { get; set; }


}