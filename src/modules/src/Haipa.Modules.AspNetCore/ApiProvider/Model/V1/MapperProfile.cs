using AutoMapper;
using Haipa.StateDb.Model;

namespace Haipa.Modules.CommonApi.Models.V1
{
    public class MapperProfile : Profile
    {
        public MapperProfile()
        {
            CreateMap<Operation, AspNetCore.ApiProvider.Model.V1.Operation>();
            CreateMap<OperationLogEntry, AspNetCore.ApiProvider.Model.V1.OperationLogEntry>();
            CreateMap<OperationResource, AspNetCore.ApiProvider.Model.V1.OperationResource>();
        }
    }
}