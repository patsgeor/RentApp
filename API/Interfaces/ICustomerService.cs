using System;
using API.DTOs.Contacts;
using API.DTOs.Customer;
using API.Helper;

namespace API.Interfaces;

public interface ICustomerService
{
    Task<PaginatedResult<CustomerDto>> GetAllAsync(CustomerParams p);
    Task<CustomerDto?> GetByIdAsync(Guid id);
    Task<List<CustomerLookupDto>> GetLookupAsync(string? search);
    Task<CustomerDto> RestoreAsync(Guid id, string currentUserId);
    Task<CustomerDto> CreateAsync(CustomerCreateDto dto, string currentUserId);
    Task<CustomerDto> UpdateAsync(Guid id, CustomerUpdateDto dto, string currentUserId);
    Task DeleteAsync(Guid id, string currentUserId);
 
    Task<ContactDto> AddContactAsync(Guid customerId, ContactCreateDto dto, string currentUserId);
    Task<ContactDto> UpdateContactAsync(Guid customerId, Guid contactId, ContactCreateDto dto, string currentUserId);
    Task RemoveContactAsync(Guid customerId, Guid contactId);
    Task<CustomerStatsDto> GetCustomerStatsAsync(Guid tenantId);
    
}
