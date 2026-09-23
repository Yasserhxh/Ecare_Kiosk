using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Identity.Client;

namespace Ecare.Infrastructure.Email;

/// <summary>
/// Envoi d'e-mail via Microsoft Graph avec authentification client-credentials (Azure AD).
/// Reprend l'approche du MailingService de mycimar-web-client (même app registration
/// "AzureAdMail", même boîte expéditrice cimar.noreplay@cimar.co.ma).
/// </summary>
public sealed class GraphEmailSender : IEmailSender
{
    private static readonly string[] Scopes = { "https://graph.microsoft.com/.default" };
    private readonly IConfidentialClientApplication _msal;
    private readonly ILogger<GraphEmailSender> _logger;

    public GraphEmailSender(IConfidentialClientApplication msal, ILogger<GraphEmailSender> logger)
    {
        _msal = msal;
        _logger = logger;
    }

    public async Task<bool> SendHtmlAsync(
        string sender,
        string to,
        string? cc,
        string subject,
        string htmlBody,
        bool highPriority = false,
        CancellationToken ct = default)
    {
        try
        {
            var token = await _msal.AcquireTokenForClient(Scopes).ExecuteAsync(ct);

            var graph = new GraphServiceClient(new DelegateAuthenticationProvider(request =>
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
                return Task.CompletedTask;
            }));

            var message = new Message
            {
                Subject = subject,
                Body = new ItemBody { ContentType = BodyType.Html, Content = htmlBody },
                ToRecipients = ParseRecipients(to),
                CcRecipients = ParseRecipients(cc),
                Importance = highPriority ? Importance.High : Importance.Normal
            };

            await graph.Users[sender]
                .SendMail(message, SaveToSentItems: false)
                .Request()
                .PostAsync(ct);

            _logger.LogInformation(
                "Alerte e-mail envoyée — To: {To}, CC: {Cc}, Sujet: {Subject}", to, cc ?? "aucun", subject);
            return true;
        }
        catch (Exception ex)
        {
            var sex = ex as ServiceException;
            _logger.LogError(ex,
                "Échec envoi alerte e-mail — To: {To}, Sujet: {Subject}, GraphCode: {Code}",
                to, subject, sex?.Error?.Code ?? "n/a");
            return false;
        }
    }

    private static List<Recipient> ParseRecipients(string? csv)
    {
        var list = new List<Recipient>();
        if (string.IsNullOrWhiteSpace(csv)) return list;

        foreach (var raw in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            list.Add(new Recipient { EmailAddress = new EmailAddress { Address = raw } });

        return list;
    }
}
