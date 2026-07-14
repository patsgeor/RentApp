using System;
using System.ComponentModel.DataAnnotations;

namespace API.DTOs.User;

public class LoginDto
{
    public string Email { get; set; }   = "";
    public string Password { get; set; } = "";

}

public class ForgotPasswordDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";
}

public class ResetPasswordDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    public string Token { get; set; } = "";

    [Required, MinLength(6)]
    public string NewPassword { get; set; } = "";

    [Required, Compare(nameof(NewPassword))]
    public string ConfirmPassword { get; set; } = "";
}

public class ChangePasswordDto
{
    [Required]
    public string CurrentPassword { get; set; } = "";

    [Required, MinLength(6)]
    public string NewPassword { get; set; } = "";

    [Required, Compare(nameof(NewPassword))]
    public string ConfirmPassword { get; set; } = "";
}