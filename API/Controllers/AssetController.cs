using API.Data.Contexts;
using API.DTOs;
using API.Entities;
using API.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers
{
    public class RentController(IRentRepository RentRepository)  : BaseApiController
    {
        [HttpGet]
        public async Task<IReadOnlyList<RentViewDto>> GetRents()
        {
            var RentsDto = await RentRepository.GetRentViewDtosAsync();
            return RentsDto;
        }
    }
}
