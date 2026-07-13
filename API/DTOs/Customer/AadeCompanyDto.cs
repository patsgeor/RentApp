using System;

namespace API.DTOs.Customer;


public class AadeCompanyDto
{
    public string Afm { get; set; } = null!;
    public string? Name { get; set; }
    public string? NameEn { get; set; }
    public string? Doy { get; set; }
    public string? DoyDescription { get; set; }
    public string? Address { get; set; }
    public string? AddressNo { get; set; }
    public string? ZipCode { get; set; }
    public string? City { get; set; }
    public string? CompanyType { get; set; }  // firmFlagDescr
    public bool IsActive { get; set; }
}