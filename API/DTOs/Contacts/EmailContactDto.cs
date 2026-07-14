using System;
using System.ComponentModel.DataAnnotations;

namespace API.DTOs.Contacts;

public class EmailContactDto
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = "";

    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, MaxLength(200)]
    public string Subject { get; set; } = "";

    [Required, MinLength(10), MaxLength(2000)]
    public string Message { get; set; } = "";
}