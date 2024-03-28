using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using Eryph.Configuration.Model;
using Eryph.StateDb.Model;

namespace Eryph.Runtime.Zero.Configuration
{
    internal class MapperProfile : Profile
    {
        public MapperProfile()
        {
            CreateMap<Project, ProjectConfigModel>();
            CreateMap<ProjectConfigModel, Project>();

            CreateMap<ProjectRoleAssignment, ProjectRoleAssignmentConfigModel>();
            CreateMap<ProjectRoleAssignmentConfigModel, ProjectRoleAssignment>();
        }
    }
}
