using AutoMapper;
using PotatoVN.Client.Model;

namespace GalgameManager.Models.MapperProfile;

public class StaffMapper : Profile
{
    public StaffMapper()
    {
        CreateMap<StaffDto, Staff>();
    }
}