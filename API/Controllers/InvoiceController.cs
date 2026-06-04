using API.Data.Contexts;
using API.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers
{
    public class InvoiceController (AppDbContext context)  : BaseApiController
    {
        // public async Task<ActionResult<IReadOnlyList<InvoiceViewDto>>> GetInvoices()
        // {
        //     var invoicesDto = await context.Invoices
        //                                 .AsNoTracking()
        //                                 .Select( i => new InvoiceViewDto
        //                                 {
        //                                     Id = i.Id,
        //                                     Afm = i.Afm,
        //                                     InvoiceDate = i.InvoiceDate,
        //                                     Amount = i.Amount,
        //                                     FundingAllocationProtocol = i.FundingAllocation!.ProtocolNumber??"",
        //                                     FundingSourceName = i.FundingAllocation.FundingSource.Name ??"",
        //                                     CreatedByMemberName = i.CreatedByMember.FirstName + " " + i.CreatedByMember.LastName,
        //                                     CreatedAt = i.CreatedAt,
        //                                     Items = i.InvoiceItems
        //                                         .Select(item => new InvoiceItemViewDto
        //                                         {
        //                                             Id = item.Id,
        //                                             Description = item.Description,
        //                                             Quantity = item.Quantity,
        //                                             UnitPrice = item.UnitPrice,
        //                                             TotalPrice = item.TotalPrice,
                                                    
        //                                             // Τραβάμε τα ονόματα από τα λεξικά μέσω των Navigation Properties
        //                                             CategoryName = item.Category.Name,
        //                                             CategoryLog = item.Category.Log,
        //                                             UnitMetricDescription = item.UnitMetric.Name,
        //                                             DepreciationMethodName = item.DepreciationMethod.Name,
        //                                             ValuationMethodName = item.ValuationMethod.Name
        //                                         }).ToList()
        //                                 }).ToListAsync();

        //     return invoicesDto;
        // }
    }
}
