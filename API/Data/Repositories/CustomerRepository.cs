using System;
using API.Data.Contexts;
using API.DTOs.Contacts;
using API.DTOs.Customer;
using API.Entities;
using API.Helper;
using API.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public class CustomerRepository(AppDbContext context, ITenantProvider tenantProvider) : ICustomerRepository
{
    // ------------------------------------------------------------------
    //  READ
    //  Tenant filtering + soft-delete are already applied globally 
    //  so no manual TenantId check is needed here.
    // ------------------------------------------------------------------
 
        public async Task<PaginatedResult<CustomerDto>> GetAllAsync(CustomerParams p)    
        {
         IQueryable<Customer> query;

        if (p.ShowDeleted == "deleted" || p.ShowDeleted == "all")
        {
       // Bypass both the soft-delete and tenant global filters, then re-apply tenant manually
            query = context.Customers
                .IgnoreQueryFilters()
                .Include(c => c.Contacts)
                .AsNoTracking()
                .Where(c => c.TenantId == tenantProvider.TenantId);

            if (p.ShowDeleted == "deleted")
                query = query.Where(c => c.IsDeleted);
        }
        else
        {
            // Default: global filter handles tenant + soft-delete
            query = context.Customers.Include(c => c.Contacts).AsNoTracking();
        }

        if (!string.IsNullOrWhiteSpace(p.Search))
        {
            var term = p.Search.Trim().ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(term) ||
                c.Afm.Contains(term) ||
                c.Contacts.Any(ct =>
                    (ct.Phone != null && ct.Phone.Contains(term)) ||
                    (ct.Email != null && ct.Email.ToLower().Contains(term))));
        }
 
        var projected = query.OrderBy(c => c.Name).Select(ProjectToDto());
 
        return await PaginationHelper.CreateAsync(projected, p.PageNumber, p.PageSize);
    }
 
 
    public async Task<CustomerDto?> GetByIdAsync(Guid id)
    {
        return await context.Customers
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(ProjectToDto())
            .FirstOrDefaultAsync();
    }
 
    public async Task<List<CustomerLookupDto>> GetLookupAsync(string? search)
    {
        var query = context.Customers.AsNoTracking().AsQueryable();
 
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(term) || c.Afm.Contains(term));
        }
 
        return await query
            .OrderBy(c => c.Name)
            .Take(20)
            .Select(c => new CustomerLookupDto { Id = c.Id, Name = c.Name, Afm = c.Afm })
            .ToListAsync();
    }
 
    public async Task<Customer?> GetEntityByIdAsync(Guid id)
    {
        return await context.Customers
            .Include(c => c.Contacts)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Customer?> GetEntityByIdIgnoreFiltersAsync(Guid id)
    {
        return await context.Customers
            .IgnoreQueryFilters()
            .Include(c => c.Contacts)
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantProvider.TenantId);
    }

    public async Task<Customer?> GetDeletedByAfmAsync(string afm)
    {
        return await context.Customers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Afm == afm && c.IsDeleted && c.TenantId == tenantProvider.TenantId);
    }
 
    public async Task<bool> AfmExistsAsync(string afm, Guid? excludingId = null)
    {
        return await context.Customers
            .AnyAsync(c => c.Afm == afm && (excludingId == null || c.Id != excludingId));
    }
 
    public async Task<bool> HasActiveContractsAsync(Guid customerId)
    {
        return await context.Contracts
            .AnyAsync(c => c.CustomerId == customerId
                        && c.Status == Enums.RentalStatus.Active);
    }

 
    // ------------------------------------------------------------------
    //  WRITE   ΤΟ SAVE ΘΑ ΤΟ ΚΆΝΩ ΜΕ IUnitOfWork.Complete()
    // ------------------------------------------------------------------
     public async Task AddAsync(Customer customer)
    {
        await context.Customers.AddAsync(customer);
    }
 
    public void Update(Customer customer)
    {
        context.Entry(customer).State = EntityState.Modified;
    }
 
    public void SoftDelete(Customer customer)
    {
        // BaseEntity already carries IsDeleted/DeletedAt/DeletedBy — the global
        // query filter means a soft-deleted customer simply stops appearing.
        customer.IsDeleted = true;
        customer.DeletedAt = DateTime.UtcNow;
        context.Entry(customer).State = EntityState.Modified;
    }
 
    // ------------------------------------------------------------------
    //  CONTACTS (sub-resource)
    // ------------------------------------------------------------------
 
    public async Task AddContactAsync(Contact contact)
    {
        await context.Contacts.AddAsync(contact);
    }
 
    public async Task<Contact?> GetContactEntityByIdAsync(Guid contactId)
    {
        return await context.Contacts.FirstOrDefaultAsync(c => c.Id == contactId);
    }
 
    public void RemoveContact(Contact contact)
    {
        context.Contacts.Remove(contact);
    }
 
    // ------------------------------------------------------------------
    //  Shared projection so list/detail endpoints stay consistent
    // ------------------------------------------------------------------
    private static System.Linq.Expressions.Expression<Func<Customer, CustomerDto>> ProjectToDto()
    {
        return c => new CustomerDto
        {
            Id = c.Id,
            Type = c.Type,
            Name = c.Name,
            Afm = c.Afm,
            Dou = c.Dou,
            Address = c.Address,
            Representative = c.Representative,
            CreatedAt = c.CreatedAt,
            IsDeleted = c.IsDeleted,
            // DeletedAt = c.DeletedAt,
            Contacts = c.Contacts.Select(ct => new ContactDto
            {
                Id = ct.Id,
                Name = ct.Name,
                Phone = ct.Phone,
                Email = ct.Email,
                CanUseAsset = ct.CanUseAsset,
                Notes = ct.Notes
            }).ToList()
        };
    }

    public async Task<CustomerStatsDto> GetCustomerStatsAsync(Guid tenantId)
    {
        return await context.Customers
            .AsNoTracking()
            .IgnoreQueryFilters() // To include soft-deleted customers in the stats
            .Where(c => c.TenantId == tenantId)
            .GroupBy(c => c.TenantId) // Grouping by a constant to get aggregate stats
            .Select(g => new CustomerStatsDto
            {
                Total = g.Count(),
                Active = g.Count(c => !c.IsDeleted),
                Inactive = g.Count(c => c.IsDeleted),
                NewThisMonth = g.Count(c => c.CreatedAt >= DateTime.UtcNow.AddMonths(-1))
            })
            .FirstOrDefaultAsync() ?? new CustomerStatsDto();
    }

     public void UpdateContact(Contact contact)
    {
        context.Entry(contact).State = EntityState.Modified;
    }

}