using System;
using API.DTOs.Contacts;
using static API.Entities.Enums;

namespace API.DTOs.Customer;

public class CustomerDto
{
    public uint RowVersion { get; set; }
    public Guid Id { get; set; }
    public CustomerType Type { get; set; }
    public string Name { get; set; } = null!;
    public string Afm { get; set; } = null!;
    public string? Dou { get; set; }
    public string? Address { get; set; }
    public string? Representative { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
 
    public List<ContactDto> Contacts { get; set; } = new();
}

public class CustomerLookupDto
{

    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Afm { get; set; } = null!;
}

public class CustomerCreateDto
{
    public CustomerType Type { get; set; }
    public required string Name { get; set; }
    public required string Afm { get; set; }
    public string? Dou { get; set; }
    public string? Address { get; set; }
    public string? Representative { get; set; }

}

public class CustomerUpdateDto
{
    public uint RowVersion { get; set; }
    public CustomerType Type { get; set; }
    public required string Name { get; set; }
    public required string Afm { get; set; }
    public string? Dou { get; set; }
    public string? Address { get; set; }
    public string? Representative { get; set; }
}

public class CustomerStatsDto
{
    public int Total { get; set; }
    public int Active { get; set; }  
    public int Inactive { get; set; }
    public int NewThisMonth { get; set; }
}
