using System;
using System.Collections.Generic;
using System.Text;

namespace Eryph.Configuration.Model
{
    public class ProjectRoleAssignmentConfigModel
    {
        public Guid Id { get; set; }

        public string IdentityId { get; set; }

        public Guid RoleId { get; set; }
    }
}
