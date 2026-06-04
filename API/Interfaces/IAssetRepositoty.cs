using System;
using API.DTOs;

namespace API.Interfaces;

public interface IRentRepository
{
    Task<IReadOnlyList<RentViewDto>> GetRentViewDtosAsync();
}
