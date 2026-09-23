using System.Threading;
using System.Threading.Tasks;

namespace Ecare.Infrastructure.Email;

/// <summary>Envoi d'e-mail HTML via Azure AD / Microsoft Graph (client credentials).</summary>
public interface IEmailSender
{
    /// <param name="sender">Boîte expéditrice (ex. cimar.noreplay@cimar.co.ma).</param>
    /// <param name="to">Destinataire(s) TO, séparés par des virgules.</param>
    /// <param name="cc">Destinataire(s) CC, séparés par des virgules (ou null).</param>
    /// <returns>true si l'envoi a réussi.</returns>
    Task<bool> SendHtmlAsync(
        string sender,
        string to,
        string? cc,
        string subject,
        string htmlBody,
        bool highPriority = false,
        CancellationToken ct = default);
}
