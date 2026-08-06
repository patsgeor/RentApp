using System;

namespace API.Interfaces;

/// <summary>Αρχείο προς επισύναψη σε email (κρατείται στη μνήμη).</summary>
public record EmailAttachment(string FileName, string ContentType, byte[] Content);

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body, bool isHtml = true, IEnumerable<string>? cc = null,
        IEnumerable<EmailAttachment>? attachments = null);

}
