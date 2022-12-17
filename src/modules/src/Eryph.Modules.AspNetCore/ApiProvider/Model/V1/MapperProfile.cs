using AutoMapper;
using Eryph.Core;


namespace Eryph.Modules.AspNetCore.ApiProvider.Model.V1
{
    public class MapperProfile : Profile
    {
        public MapperProfile()
        {
            CreateMap<StateDb.Model.Operation, Operation>();

            CreateMap<StateDb.Model.OperationProject, Project>()
                .Flatten(x => x.Project);


            CreateMap<StateDb.Model.OperationLogEntry, OperationLogEntry>();
            CreateMap<StateDb.Model.OperationResource, OperationResource>();
            CreateMap<StateDb.Model.Project, Project>();

        }
    }
}