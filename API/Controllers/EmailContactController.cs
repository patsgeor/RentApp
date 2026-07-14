using System;
using API.DTOs.Contacts;
using API.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

public class EmailContactController (IEmailService emailService, IConfiguration config): BaseApiController
{

    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] EmailContactDto dto)
    {
        var adminEmail = config["AdminEmail"] ?? "erp.rentapp@gmail.com";

        var body = $"""
            <h2>Νέο μήνυμα επικοινωνίας</h2>
            <p><strong>Από:</strong> {dto.Name} ({dto.Email})</p>
            <p><strong>Θέμα:</strong> {dto.Subject}</p>
            <hr/>
            <p>{dto.Message.Replace("\n", "<br/>")}</p>
            """;

        await emailService.SendEmailAsync(adminEmail, $"[RentApp] {dto.Subject}", body, isHtml: true);

        // Επιβεβαίωση στον αποστολέα
        var confirm = $"""
            <p>Γεια σας <strong>{dto.Name}</strong>,</p>
            <p>Λάβαμε το μήνυμά σας με θέμα <em>"{dto.Subject}"</em>.</p>
            <p>Θα επικοινωνήσουμε μαζί σας εντός <strong>1 εργάσιμης ημέρας</strong>.</p>
            <br/>
            <p>Η ομάδα RentApp</p>
            """;

        await emailService.SendEmailAsync(dto.Email, "Λάβαμε το μήνυμά σας — RentApp", confirm, isHtml: true);

        return Ok(new { message = "Το μήνυμά σας στάλθηκε." });
    }
}
