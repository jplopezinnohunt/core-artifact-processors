using System.Net;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure.Messaging.ServiceBus;

namespace VendorMdm.Artifacts.Functions;

/// <summary>
/// Function A - HTTP Ingestion Function
/// Receives vendor data from web app and queues it for SAP processing
/// </summary>
public class SapVendorIngestionFunction
{
    private readonly ILogger _logger;
    private readonly ServiceBusClient _serviceBusClient;

    public SapVendorIngestionFunction(
        ILoggerFactory loggerFactory,
        ServiceBusClient serviceBusClient)
    {
        _logger = loggerFactory.CreateLogger<SapVendorIngestionFunction>();
        _serviceBusClient = serviceBusClient;
    }

    /// <summary>
    /// HTTP endpoint to receive vendor creation requests
    /// POST /api/sap/vendor/create
    /// </summary>
    [Function("SapVendorCreate")]
    public async Task<HttpResponseData> CreateVendor(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "sap/vendor/create")]
        HttpRequestData req)
    {
        _logger.LogInformation("SAP Vendor Create request received");

        try
        {
            // Parse request body
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonConvert.DeserializeObject<SapVendorRequest>(requestBody);

            if (request == null || request.Vendor == null)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(new
                {
                    Error = "Invalid request payload",
                    Message = "Vendor data is required"
                });
                return badResponse;
            }

            // Validate required fields
            var validationErrors = ValidateVendorRequest(request);
            if (validationErrors.Any())
            {
                var validationResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await validationResponse.WriteAsJsonAsync(new
                {
                    Error = "Validation failed",
                    Errors = validationErrors
                });
                return validationResponse;
            }

            // Generate correlation ID
            var correlationId = Guid.NewGuid().ToString();

            // Enrich message with metadata
            var message = new SapVendorMessage
            {
                CorrelationId = correlationId,
                Timestamp = DateTime.UtcNow,
                Operation = "CREATE",
                Vendor = request.Vendor,
                UserContext = request.UserContext
            };

            // Publish to Service Bus queue
            await PublishToServiceBusAsync("sap-vendor-create", message);

            _logger.LogInformation(
                "Vendor creation request queued. CorrelationId: {CorrelationId}, Role: {Role}",
                correlationId,
                request.UserContext?.Role ?? "Unknown");

            // Return 202 Accepted (async processing)
            var response = req.CreateResponse(HttpStatusCode.Accepted);
            await response.WriteAsJsonAsync(new
            {
                CorrelationId = correlationId,
                Status = "queued",
                Message = "Vendor creation request submitted to SAP",
                EstimatedProcessingTime = "2-5 seconds"
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing vendor creation request");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new
            {
                Error = "Internal server error",
                Message = ex.Message
            });
            return errorResponse;
        }
    }

    /// <summary>
    /// HTTP endpoint to receive vendor update requests
    /// POST /api/sap/vendor/update
    /// </summary>
    [Function("SapVendorUpdate")]
    public async Task<HttpResponseData> UpdateVendor(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "sap/vendor/update")]
        HttpRequestData req)
    {
        _logger.LogInformation("SAP Vendor Update request received");

        try
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonConvert.DeserializeObject<SapVendorRequest>(requestBody);

            if (request == null || request.Vendor == null)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(new { Error = "Invalid request payload" });
                return badResponse;
            }

            var correlationId = Guid.NewGuid().ToString();

            var message = new SapVendorMessage
            {
                CorrelationId = correlationId,
                Timestamp = DateTime.UtcNow,
                Operation = "UPDATE",
                Vendor = request.Vendor,
                UserContext = request.UserContext
            };

            await PublishToServiceBusAsync("sap-vendor-update", message);

            _logger.LogInformation("Vendor update request queued. CorrelationId: {CorrelationId}", correlationId);

            var response = req.CreateResponse(HttpStatusCode.Accepted);
            await response.WriteAsJsonAsync(new
            {
                CorrelationId = correlationId,
                Status = "queued",
                Message = "Vendor update request submitted to SAP"
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing vendor update request");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { Error = ex.Message });
            return errorResponse;
        }
    }

    /// <summary>
    /// Publish message to Service Bus queue
    /// </summary>
    private async Task PublishToServiceBusAsync(string queueName, SapVendorMessage message)
    {
        var sender = _serviceBusClient.CreateSender(queueName);
        
        try
        {
            var messageBody = JsonConvert.SerializeObject(message);
            var serviceBusMessage = new ServiceBusMessage(messageBody)
            {
                MessageId = message.CorrelationId,
                ContentType = "application/json",
                CorrelationId = message.CorrelationId
            };

            // Enable duplicate detection
            serviceBusMessage.ApplicationProperties["DeduplicationId"] = message.CorrelationId;

            await sender.SendMessageAsync(serviceBusMessage);
            
            _logger.LogInformation(
                "Message published to queue {QueueName}. MessageId: {MessageId}",
                queueName,
                message.CorrelationId);
        }
        finally
        {
            await sender.DisposeAsync();
        }
    }

    /// <summary>
    /// Validate vendor request
    /// </summary>
    private List<string> ValidateVendorRequest(SapVendorRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Vendor.Name))
            errors.Add("Vendor name is required");

        if (string.IsNullOrWhiteSpace(request.Vendor.TaxId))
            errors.Add("Tax ID is required");

        if (request.UserContext == null)
            errors.Add("User context is required");

        if (request.UserContext != null && string.IsNullOrWhiteSpace(request.UserContext.Role))
            errors.Add("User role is required");

        if (request.UserContext?.Role == "Approver" && string.IsNullOrWhiteSpace(request.UserContext.AzureAdUserId))
            errors.Add("Azure AD User ID is required for approvers");

        if (request.UserContext?.Role == "Vendor" && string.IsNullOrWhiteSpace(request.UserContext.InvitationToken))
            errors.Add("Invitation token is required for vendors");

        return errors;
    }
}

/// <summary>
/// Request model for SAP vendor operations
/// </summary>
public class SapVendorRequest
{
    public VendorData Vendor { get; set; } = new();
    public UserContext? UserContext { get; set; }
}

/// <summary>
/// Vendor data model
/// </summary>
public class VendorData
{
    public string Name { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? BankAccount { get; set; }
    public string? BankName { get; set; }
    public string? Currency { get; set; }
    public string? PaymentTerms { get; set; }
}

/// <summary>
/// User context for authentication routing
/// </summary>
public class UserContext
{
    public string Role { get; set; } = string.Empty; // "Approver" or "Vendor"
    public string UserId { get; set; } = string.Empty; // Portal user ID
    public string? AzureAdUserId { get; set; } // Present only for approvers
    public string? Email { get; set; }
    public string? InvitationToken { get; set; } // Present only for vendors
}

/// <summary>
/// Message model for Service Bus
/// </summary>
public class SapVendorMessage
{
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Operation { get; set; } = string.Empty; // CREATE, UPDATE, DELETE
    public VendorData Vendor { get; set; } = new();
    public UserContext? UserContext { get; set; }
}
