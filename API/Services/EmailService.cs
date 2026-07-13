using System;
using System.Net;
using System.Net.Mail;
using API.Interfaces;

namespace API.Services;

public class EmailService: IEmailService
{
    private readonly IConfiguration configuration;

    public EmailService(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public async Task SendEmailAsync(
        string to,
        string subject,
        string body,
        bool isHtml = true,
        IEnumerable<string>? cc = null)
    {
        var smtpHost = configuration["Email:SmtpHost"];
        var smtpPort = int.Parse(configuration["Email:SmtpPort"]!);
        var username = configuration["Email:Username"];
        var password = configuration["Email:Password"];

        using var client = new SmtpClient(smtpHost, smtpPort)
        {
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(username, password),
            EnableSsl = true
        };

         using var message = new MailMessage
        {
            From = new MailAddress(username!),
            Subject = subject,
            Body = body,
            IsBodyHtml = isHtml
        };

        message.To.Add(to);
        if (cc != null)
        {
            foreach (var ccAddress in cc)
            {
                 if (!string.IsNullOrWhiteSpace(ccAddress))
                message.CC.Add(ccAddress);
            }
        }

        string disclaimer = $@"
            <hr>

           <p>
            Το παρόν μήνυμα ηλεκτρονικού ταχυδρομείου και τυχόν συνημμένα αρχεία περιέχουν
            εμπιστευτικές πληροφορίες και προορίζονται αποκλειστικά για τον παραλήπτη.
            Εάν λάβατε το μήνυμα αυτό εκ παραδρομής, παρακαλούμε μην το χρησιμοποιήσετε,
            μην το αντιγράψετε, μην το κοινοποιήσετε και ενημερώστε άμεσα τον αποστολέα.
            Κάθε μη εξουσιοδοτημένη χρήση απαγορεύεται.
            </p>

            <p>
            Για οποιαδήποτε διευκρίνιση ή πληροφορία μπορείτε να επικοινωνήσετε με
            Email: {cc?.ElementAt(0)}<br/>
            </p>

            <p>
            <strong>Παρακαλούμε μην απαντάτε στο παρόν μήνυμα καθώς έχει αποσταλεί αυτόματα.</strong>
            </p>


            <p>
            CONFIDENTIALITY DISCLAIMER:
            This email and any attachments contain confidential information and are intended
            solely for the use of the individual(s) to whom they are addressed.
            If you are not the intended recipient, please do not read, copy, disclose,
            forward or use this message. Please notify the sender immediately and delete it
            from your system. Any unauthorized use is strictly prohibited.
            </p>

            <p>
            For any questions or further information, please contact:<br/>
            Email: {cc?.ElementAt(0)}<br/>
            </p>

            <p>
            <strong>Please do not reply to this email as it has been generated automatically.</strong>
            </p>";

            message.Body += disclaimer;

        await client.SendMailAsync(message);
    }
}