using System;
using API.Data.Contexts;
using API.DTOs;
using API.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public class RentRepository(AppDbContext context) : IRentRepository
{
    public async Task<IReadOnlyList<RentViewDto>> GetRentViewDtosAsync()
    {
         var RentsDto = await context.Rents
                                .AsNoTracking() // για να μην παρακολουθείται η κατάσταση των οντοτήτων, βελτιώνοντας την απόδοση σε περιπτώσεις όπου δεν απαιτείται ενημέρωση
                                .Where(a => a.IsDeleted==false)
                                .Select(a => new RentViewDto
                                {
                                    Id = a.Id,
                                    Description=a.InvoiceItem.Description,
                                    LogCategory=a.InvoiceItem.Category.Log+" - "+a.InvoiceItem.Category.Name,
                                    Notes = a.Notes,
                                    CreatedByMemberName = a.CreatedByMember.FirstName + " " + a.CreatedByMember.LastName,
                                    MonadaName = a.Monada.Name,
                                    InvoiceItemId = a.InvoiceItemId,
                                    SerialNumber = a.SerialNumber,
                                    InitialValue = a.InitialValue,
                                    CurrentValue = a.CurrentValue,
                                    ResidualValue = a.ResidualValue,
                                    AcquiredDate = a.AcquiredDate,
                                    UsefulLifeYears = a.UsefulLifeYears,
                                    IsLocked = a.IsLocked,
                                    CreatedAt = a.CreatedAt,
                                    ModifiedAt = a.ModifiedAt
                                })
                                .OrderBy(a => a.InvoiceItemId)
                                .ToListAsync();
            return RentsDto;
    }
}
