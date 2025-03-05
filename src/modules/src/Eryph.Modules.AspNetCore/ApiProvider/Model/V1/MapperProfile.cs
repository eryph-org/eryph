using System.Linq;
using AutoMapper;
using Eryph.Core;
using Eryph.StateDb.Model;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model.V1;

public class MapperProfile : Profile
{
    public MapperProfile()
    {
        CreateMap<StateDb.Model.OperationModel, Operation>()
            .ForMember(x => x.Result, m => m.MapFrom<OperationResultValueResolver>());

        CreateMap<StateDb.Model.OperationProjectModel, Project>()
            .Flatten(x => x.Project);

        CreateMap<StateDb.Model.OperationLogEntry, OperationLogEntry>();
        CreateMap<StateDb.Model.OperationResourceModel, OperationResource>();
        CreateMap<StateDb.Model.Project, Project>();
        CreateMap<StateDb.Model.OperationTaskModel, OperationTask>()
            .ForMember(
                x => x.Progress,
                m => m.MapFrom(x => x.Status == OperationTaskStatus.Completed
                    ? 100
                    : x.Progress.Select(p => p.Progress).DefaultIfEmpty().Max()))
            .ForMember(x => x.Reference,
                m =>
                {
                    m.PreCondition(x => x.ReferenceType.HasValue);
                    m.MapFrom(s => new OperationTaskReference
                        {
                            Id = s.ReferenceId!,
                            Type = s.ReferenceType!.Value,
                            ProjectName = s.ReferenceProjectName!
                        });
                });
    }
}
