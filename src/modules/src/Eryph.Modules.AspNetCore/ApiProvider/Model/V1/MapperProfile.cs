using AutoMapper;
using Eryph.Core;


namespace Eryph.Modules.AspNetCore.ApiProvider.Model.V1
{
    public class MapperProfile : Profile
    {
        public MapperProfile()
        {
            CreateMap<StateDb.Model.OperationModel, Operation>();

            CreateMap<StateDb.Model.OperationProjectModel, Project>()
                .Flatten(x => x.Project);


            CreateMap<StateDb.Model.OperationLogEntry, OperationLogEntry>();
            CreateMap<StateDb.Model.OperationResourceModel, OperationResource>();
            CreateMap<StateDb.Model.Project, Project>();
            CreateMap<StateDb.Model.OperationTaskModel, OperationTask>()
                .ForMember(x => x.ParentTask, m => m.MapFrom(x => x.ParentTaskId));


        }
    }
}