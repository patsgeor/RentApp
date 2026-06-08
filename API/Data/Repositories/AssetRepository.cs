using System;
using API.Data.Contexts;
using API.DTOs;
using API.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public class RentRepository(AppDbContext context) : IRentRepository
{
    public Task<IReadOnlyList<RentViewDto>> GetRentViewDtosAsync()
    {
        throw new NotImplementedException();
    }
}
