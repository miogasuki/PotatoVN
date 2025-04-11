using AutoMapper;
using GalgameManager.Core.Helpers;
using GalgameManager.Server.Contracts;
using GalgameManager.Server.Models;

namespace GalgameManager.Server.Services;

public class StaffService(
    IStaffRepository staffRepository,
    IGalgameRepository gameRepository,
    IUserRepository userRepository,
    IMapper mapper) : IStaffService
{
    public async Task<PagedResult<Staff>> GetStaffsAsync(int userId, long lastChanged, int pageIndex, int pageSize,
        bool includeDeleted = true)
    {
        (List<Staff> staffs, int cnt) tmp =
            await staffRepository.GetStaffsAsync(userId, lastChanged, pageIndex, pageSize, includeDeleted);
        return new PagedResult<Staff>(tmp.staffs, pageIndex, pageSize, tmp.cnt);
    }

    public async Task<Staff> UpsertAsync(int userId, StaffUpdateDto dto)
    {
        User? user = await userRepository.GetUserAsync(userId);
        if (user is null) throw new UnauthorizedAccessException("user not found");
        Staff staff = new();
        var isNew = dto.Id is 0 or null;
        if (dto.Id != 0 && dto.Id is not null) staff = await staffRepository.GetStaffWithIdAsync(dto.Id.Value);
        else
        {
            staff.UserId = userId;
            await staffRepository.Upsert(staff);
        }
        try
        {
            if (staff.UserId != 0 && staff.UserId != userId)
                throw new UnauthorizedAccessException("staff is not owned by user");
            staff.UserId = userId;
            staff.ImageLoc = dto.ImageLoc ?? staff.ImageLoc;
            staff.ExternalImageLink = dto.ExternalImageLink ?? staff.ExternalImageLink;
            if (dto.StaffGames is not null)
            {
                staff.StaffGames = new List<StaffGame>(dto.StaffGames.Select(mapper.Map<StaffGame>));
                foreach(StaffGame sg in staff.StaffGames)
                    sg.StaffId = staff.Id;
                List<Galgame> games =
                    await gameRepository.GetGalgamesAsync(staff.StaffGames.Select(sg => sg.GameId).ToList());
                if (games.Count != staff.StaffGames.Count)
                {
                    throw new KeyNotFoundException(
                        $"game {staff.StaffGames.FirstOrDefault(sg => games.All(g => g.Id != sg.GameId))!.GameId} is not founded.");
                }
            }
            staff.IsDeleted = dto.IsDeleted ?? staff.IsDeleted;
            staff.BgmId = dto.BgmId ?? staff.BgmId;
            staff.VndbId = dto.VndbId ?? staff.VndbId;
            staff.YmgalId = dto.YmgalId ?? staff.YmgalId;
            staff.JapaneseName = dto.JapaneseName ?? staff.JapaneseName;
            staff.EnglishName = dto.EnglishName ?? staff.EnglishName;
            staff.ChineseName = dto.ChineseName ?? staff.ChineseName;
            staff.Gender = dto.Gender ?? staff.Gender;
            staff.Description = dto.Description ?? staff.Description;
            staff.BirthDateTimestamp = dto.BirthDateTimestamp ?? staff.BirthDateTimestamp;
            staff.LastModifyTimestamp = DateTime.Now.ToUnixTime();

            await staffRepository.Upsert(staff);
            user.LastStaffChangedTimeStamp = DateTime.Now.ToUnixTime();
            await userRepository.UpdateUserAsync(user);
            return staff;
        }
        catch
        {
            if (isNew) await staffRepository.Delete(staff);
            throw;
        }
    }
}