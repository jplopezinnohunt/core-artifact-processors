using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace VendorMdm.Artifacts.Functions;

/// <summary>
/// SAP HTTP Webhook Function
/// Fallback option for Phase 1 - SAP can POST status updates here
/// Will be replaced by Event Hubs in Phase 2
/// </summary>
public class SapHttpWebhookFunction
{
    private readonly ILogger _logger;

    public SapHttpWebhookFunction(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<SapHttpWebhookFunction>();
    }

    /// <summary>
    /// HTTP endpoint for SAP to POST status updates
    /// POST /api/sap/webhook/status
    /// </summary>
    [Function("SapStatusWebhook")]
    public async Task<HttpResponseData> ReceiveStatus(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "sap/webhook/status")]
        HttpRequestData req)
    {
        _logger.LogInformation("SAP status webhook received");

        try
        {
            // Parse request body
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var statusUpdate = JsonConvert.DeserializeObject<SapStatusWebhookPayload>(requestBody);

            if (statusUpdate == null)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(new { Error = "Invalid payload" });
                return badResponse;
            }

            _logger.LogInformation(
                "Status update received. CorrelationId: {CorrelationId}, Status: {Status}, Vendor: {VendorNumber}",
                statusUpdate.CorrelationId,
                statusUpdate.Status,
                statusUpdate.VendorNumber ?? "N/A");

            // TODO: Phase 1 - Store status in Cosmos DB or send to SignalR
            // For now, just log
            await ProcessStatusUpdateAsync(statusUpdate);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Message = "Status update received",
                CorrelationId = statusUpdate.CorrelationId
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SAP status webhook");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { Error = ex.Message });
            return errorResponse;
        }
    }

    /// <summary>
    /// Test endpoint to verify SAP connectivity
    /// POST /api/sap/webhook/test
    /// </summary>
    [Function("SapWebhookTest")]
    public async Task<HttpResponseData> TestWebhook(
        [HttpTrigger(AuthorizationLevel.Function, "post", "get", Route = "sap/webhook/test")]
        HttpRequestData req)
    {
        _logger.LogInformation("SAP webhook test endpoint called");

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            Message = "SAP webhook is reachable",
            Timestamp = DateTime.UtcNow,
            Environment = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") ?? "Unknown",
            Note = "Use this endpoint to test connectivity from SAP (Z_TEST_AZURE_PUSH)"
        });

        return response;
    }

    /// <summary>
    /// Process status update
    /// </summary>
    private async Task ProcessStatusUpdateAsync(SapStatusWebhookPayload statusUpdate)
    {
        // TODO: Phase 1 implementation options:
        // 1. Store in Cosmos DB for polling by frontend
        // 2. Send to SignalR for real-time notification
        // 3. Publish to Event Hubs (Phase 2)

        _logger.LogInformation("Processing status update for correlation ID {CorrelationId}", 
            statusUpdate.CorrelationId);

        // Mock implementation
        await Task.CompletedTask;
    }
}

/// <summary>
/// Payload model for SAP status webhook
/// This matches the format that SAP ABAP program will POST
/// </summary>
public class SapStatusWebhookPayload
{
    public string CorrelationId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // "SUCCESS" or "ERROR"
    public string? VendorNumber { get; set; }
    public string? Message { get; set; }
    public List<SapError>? Errors { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// SAP error model
/// </summary>
public class SapError
{
    public string Type { get; set; } = string.Empty; // "E", "W", "I", "S"
    public string Id { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
