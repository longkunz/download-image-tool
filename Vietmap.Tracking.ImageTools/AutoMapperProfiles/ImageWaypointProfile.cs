using AutoMapper;
using Vietmap.Tracking.Core;
using Vietmap.Tracking.ImageTools.Models;

namespace Vietmap.Tracking.ImageTools.MapperProfiles
{
    public class ImageWaypointProfile : Profile
    {
        public ImageWaypointProfile()
        {
            CreateMap<ImageWaypointOmitted, ImageInfo>()
                .ForMember(dest => dest.VehicleId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Time, opt => opt.MapFrom(src => src.Time))
                .ForMember(dest => dest.Cam, opt => opt.MapFrom(src => src.Cam))
                .ForMember(dest => dest.Url, opt => opt.MapFrom(src => src.Url));
        }
    }
}
