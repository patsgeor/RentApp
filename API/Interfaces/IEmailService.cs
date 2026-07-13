using System;

namespace API.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body, bool isHtml = true, IEnumerable<string>? cc = null);

}
