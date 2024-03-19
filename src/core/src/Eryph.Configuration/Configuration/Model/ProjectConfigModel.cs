using System;
using System.Collections.Generic;
using System.Text;

namespace Eryph.Configuration.Model
{
    public class ProjectConfigModel
    {
        public Guid Id { get; set; }

        public string Name { get; set; }

        public Guid TenantId { get; set; }

        public ProjectRoleAssignmentConfigModel[] Assignments { get; set; }
    }
}
