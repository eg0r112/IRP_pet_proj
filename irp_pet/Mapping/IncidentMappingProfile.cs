using AutoMapper;
using irp_pet.DTOs;
using irp_pet.Models;

namespace irp_pet.Mapping;

public class IncidentMappingProfile : Profile
{
    public IncidentMappingProfile()
    {
        CreateMap<Incident, IncidentListItemDto>()
            .ForMember(d => d.ServiceKey, o => o.MapFrom(s => s.Service.Key));

        CreateMap<IncidentTimeline, TimelineItemDto>()
            .ForMember(d => d.ActorEmail, o => o.MapFrom(s => s.Actor != null ? s.Actor.Email : null));

        CreateMap<Incident, IncidentDetailDto>()
            .IncludeBase<Incident, IncidentListItemDto>()
            .ForMember(d => d.Timeline, o => o.MapFrom(s => s.Timeline.OrderBy(t => t.CreatedAtUtc)));
    }
}
