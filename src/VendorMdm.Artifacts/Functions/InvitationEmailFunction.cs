using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
// using Azure.Communication.Email; // TODO: Add package when implementing email service
using Microsoft.Azure.Functions.Worker.Http;
using VendorMdm.Shared.Services.EmailTemplates;
using VendorMdm.Shared.Models.Email;

namespace VendorMdm.Artifacts.Functions;

/// <summary>
/// Azure Function to send vendor invitation emails
/// Triggered by Service Bus queue or HTTP endpoint
/// </summary>
public class InvitationEmailFunction
{
    private readonly ILogger _logger;
    private readonly IEmailTemplate<InvitationEmailData> _emailTemplate;
    // TODO: Inject Azure Communication Services Email Client or SendGrid
    // private readonly EmailClient _emailClient;

    public InvitationEmailFunction(
        ILoggerFactory loggerFactory,
        IEmailTemplate<InvitationEmailData> emailTemplate)
    {
        _logger = loggerFactory.CreateLogger<InvitationEmailFunction>();
        _emailTemplate = emailTemplate;
    }

    /// <summary>
    /// Service Bus triggered function to send invitation email
    /// Message format: { "invitationId", "vendorName", "email", "token", "expiresAt", "invitedByName" }
    /// </summary>
    [Function("SendInvitationEmail")]
    public async Task SendInvitationEmailFromQueue(
        [ServiceBusTrigger("invitation-emails", Connection = "ServiceBusConnection")] string message)
    {
        _logger.LogInformation("Processing invitation email from Service Bus: {Message}", message);

        try
        {
            var invitationData = JsonConvert.DeserializeObject<InvitationEmailRequest>(message);
            
            if (invitationData == null)
            {
                _logger.LogError("Invalid invitation email message format");
                return;
            }

            await SendInvitationEmailAsync(invitationData);
            
            _logger.LogInformation(
                "Invitation email sent successfully to {Email} for invitation {InvitationId}", 
                invitationData.Email, 
                invitationData.InvitationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending invitation email");
            throw; // Let Service Bus handle retry
        }
    }

    /// <summary>
    /// HTTP triggered function for manual/testing email sending
    /// </summary>
    [Function("SendInvitationEmailHttp")]
    public async Task<HttpResponseData> SendInvitationEmailHttp(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "invitation/send-email")] 
        HttpRequestData req)
    {
        _logger.LogInformation("Processing manual invitation email request");

        try
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var invitationData = JsonConvert.DeserializeObject<InvitationEmailRequest>(requestBody);

            if (invitationData == null)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Invalid request payload");
                return badResponse;
            }

            await SendInvitationEmailAsync(invitationData);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { 
                Success = true, 
                Message = $"Invitation email sent to {invitationData.Email}" 
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending invitation email");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Core email sending logic
    /// </summary>
    private async Task SendInvitationEmailAsync(InvitationEmailRequest data)
    {
        _logger.LogInformation("Sending invitation email to {Email}", data.Email);

        // Generate invitation link
        var baseUrl = Environment.GetEnvironmentVariable("APP_BASE_URL") ?? "https://vendor-portal.company.com";
        var invitationLink = $"{baseUrl}/invitation/register/{data.Token}";

        // Prepare email data for template
        var emailData = new InvitationEmailData
        {
            VendorName = data.VendorName,
            Email = data.Email,
            InvitationLink = invitationLink,
            ExpiresAt = data.ExpiresAt,
            InvitedByName = data.InvitedByName,
            CompanyName = data.CompanyName ?? "Our Company"
        };

        // Generate email content using template
        var emailSubject = _emailTemplate.GetSubject(emailData);
        var emailBody = _emailTemplate.GetHtmlBody(emailData);

        // TODO: Replace with actual email service (Azure Communication Services or SendGrid)
        // For now, logging the email (mock implementation)
        _logger.LogInformation("===== INVITATION EMAIL =====");
        _logger.LogInformation("To: {Email}", data.Email);
        _logger.LogInformation("Subject: {Subject}", emailSubject);
        _logger.LogInformation("Invitation Link: {Link}", invitationLink);
        _logger.LogInformation("Expires: {ExpiresAt}", data.ExpiresAt);
        _logger.LogInformation("============================");

        // PRODUCTION IMPLEMENTATION:
        // await _emailClient.SendAsync(
        //     WaitUntil.Completed,
        //     senderAddress: "noreply@company.com",
        //     recipientAddress: data.Email,
        //     subject: emailSubject,
        //     htmlContent: emailBody
        // );

        await Task.CompletedTask; // Placeholder
    }

}

/// <summary>
/// Request model for invitation email
/// </summary>
public class InvitationEmailRequest
{
    public string InvitationId { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string ExpiresAt { get; set; } = string.Empty;
    public string InvitedByName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? Notes { get; set; }
}
