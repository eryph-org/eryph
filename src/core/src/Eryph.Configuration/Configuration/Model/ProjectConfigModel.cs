using System;
using System.Collections.Generic;
using System.Text;

namespace Eryph.Configuration.Model
{
    public class ProjectConfigModel
    {
        public string Name { get; set; }

        public bool Deleted { get; set; }

        public Guid TenantId { get; set; }

        public ProjectRoleAssignmentConfigModel[] Assignments { get; set; }
    }
}
