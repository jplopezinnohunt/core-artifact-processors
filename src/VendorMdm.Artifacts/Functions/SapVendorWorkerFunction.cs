using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure.Security.KeyVault.Secrets;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;

namespace VendorMdm.Artifacts.Functions;

/// <summary>
/// Function B - Service Bus Worker Function
/// Processes vendor messages from queue and executes SAP BAPI calls
/// Implements hybrid authentication (role-based)
/// </summary>
public class SapVendorWorkerFunction
{
    private readonly ILogger _logger;
    private readonly SecretClient _keyVaultClient;
    private readonly EventHubProducerClient _eventHubClient;

    public SapVendorWorkerFunction(
        ILoggerFactory loggerFactory,
        SecretClient keyVaultClient,
        EventHubProducerClient eventHubClient)
    {
        _logger = loggerFactory.CreateLogger<SapVendorWorkerFunction>();
        _keyVaultClient = keyVaultClient;
        _eventHubClient = eventHubClient;
    }

    /// <summary>
    /// Service Bus trigger for vendor creation
    /// </summary>
    [Function("SapVendorCreateWorker")]
    public async Task ProcessVendorCreate(
        [ServiceBusTrigger("sap-vendor-create", Connection = "ServiceBusConnection")]
        string message)
    {
        _logger.LogInformation("Processing vendor creation from Service Bus");

        try
        {
            var vendorMessage = JsonConvert.DeserializeObject<SapVendorMessage>(message);
            
            if (vendorMessage == null)
            {
                _logger.LogError("Invalid message format");
                throw new InvalidOperationException("Invalid message format");
            }

            await ProcessVendorOperationAsync(vendorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing vendor creation");
            throw; // Let Service Bus handle retry
        }
    }

    /// <summary>
    /// Service Bus trigger for vendor update
    /// </summary>
    [Function("SapVendorUpdateWorker")]
    public async Task ProcessVendorUpdate(
        [ServiceBusTrigger("sap-vendor-update", Connection = "ServiceBusConnection")]
        string message)
    {
        _logger.LogInformation("Processing vendor update from Service Bus");

        try
        {
            var vendorMessage = JsonConvert.DeserializeObject<SapVendorMessage>(message);
            
            if (vendorMessage == null)
            {
                _logger.LogError("Invalid message format");
                throw new InvalidOperationException("Invalid message format");
            }

            await ProcessVendorOperationAsync(vendorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing vendor update");
            throw;
        }
    }

    /// <summary>
    /// Core processing logic for vendor operations
    /// </summary>
    private async Task ProcessVendorOperationAsync(SapVendorMessage message)
    {
        var startTime = DateTime.UtcNow;
        
        _logger.LogInformation(
            "Processing {Operation} for correlation ID {CorrelationId}, User Role: {Role}",
            message.Operation,
            message.CorrelationId,
            message.UserContext?.Role ?? "Unknown");

        try
        {
            // Step 1: Detect user role and select authentication method
            var authMethod = DetermineAuthenticationMethod(message.UserContext);
            
            _logger.LogInformation(
                "Authentication method selected: {AuthMethod} for role {Role}",
                authMethod,
                message.UserContext?.Role);

            // Step 2: Establish SAP connection based on authentication method
            ISapConnection sapConnection;
            
            if (authMethod == AuthenticationMethod.IdentityPropagation)
            {
                // Internal approver - use SNC with X.509 certificate
                sapConnection = await CreateSncConnectionAsync(message.UserContext!.AzureAdUserId!);
            }
            else
            {
                // External vendor or fallback - use system account
                sapConnection = await CreateBasicAuthConnectionAsync();
            }

            // Step 3: Execute BAPI
            var result = await ExecuteBapiAsync(sapConnection, message);

            // Step 4: For vendors, store mapping (portal user ID <-> SAP vendor number)
            if (message.UserContext?.Role == "Vendor" && result.Success)
            {
                await StoreVendorMappingAsync(
                    message.UserContext.UserId,
                    result.SapVendorNumber!);
            }

            // Step 5: Publish status event to Event Hubs
            await PublishStatusEventAsync(new SapStatusEvent
            {
                CorrelationId = message.CorrelationId,
                Status = result.Success ? "success" : "failure",
                SapVendorNumber = result.SapVendorNumber,
                Errors = result.Errors,
                Timestamp = DateTime.UtcNow,
                ProcessingDuration = (DateTime.UtcNow - startTime).TotalSeconds,
                AuthMethod = authMethod.ToString()
            });

            var duration = (DateTime.UtcNow - startTime).TotalSeconds;
            _logger.LogInformation(
                "Vendor {Operation} completed. Success: {Success}, Duration: {Duration}s, SAP Vendor: {VendorNumber}",
                message.Operation,
                result.Success,
                duration,
                result.SapVendorNumber ?? "N/A");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing SAP BAPI");
            
            // Publish failure event
            await PublishStatusEventAsync(new SapStatusEvent
            {
                CorrelationId = message.CorrelationId,
                Status = "failure",
                Errors = new List<string> { ex.Message },
                Timestamp = DateTime.UtcNow,
                ProcessingDuration = (DateTime.UtcNow - startTime).TotalSeconds
            });

            throw; // Trigger Service Bus retry
        }
    }

    /// <summary>
    /// Determine authentication method based on user role
    /// </summary>
    private AuthenticationMethod DetermineAuthenticationMethod(UserContext? userContext)
    {
        if (userContext?.Role == "Approver" && !string.IsNullOrWhiteSpace(userContext.AzureAdUserId))
        {
            return AuthenticationMethod.IdentityPropagation;
        }

        return AuthenticationMethod.SystemAccount;
    }

    /// <summary>
    /// Create SNC connection for internal approvers (identity propagation)
    /// TODO: Implement actual SNC connection with SAP .NET Connector
    /// </summary>
    private async Task<ISapConnection> CreateSncConnectionAsync(string azureAdUserId)
    {
        _logger.LogInformation("Creating SNC connection for user {UserId}", azureAdUserId);

        // TODO: Phase 3 implementation
        // 1. Retrieve user's Azure AD token
        // 2. Exchange token for X.509 certificate
        // 3. Create SAP NCo connection with SNC parameters
        // 4. Return connection

        // For now, return mock connection
        await Task.CompletedTask;
        return new MockSapConnection("SNC", azureAdUserId);
    }

    /// <summary>
    /// Create basic auth connection for vendors (system account)
    /// TODO: Implement actual SAP .NET Connector connection
    /// </summary>
    private async Task<ISapConnection> CreateBasicAuthConnectionAsync()
    {
        _logger.LogInformation("Creating basic auth connection with system account");

        string hostname, systemNumber, client, username, password;

        // Try to get credentials from Key Vault first (Azure), fallback to environment variables (local)
        if (_keyVaultClient != null)
        {
            try
            {
                // Retrieve SAP credentials from Key Vault
                var hostnameSecret = await _keyVaultClient.GetSecretAsync("SAP-Hostname");
                var systemNumberSecret = await _keyVaultClient.GetSecretAsync("SAP-SystemNumber");
                var clientSecret = await _keyVaultClient.GetSecretAsync("SAP-Client");
                var usernameSecret = await _keyVaultClient.GetSecretAsync("SAP-SystemAccount-Username");
                var passwordSecret = await _keyVaultClient.GetSecretAsync("SAP-SystemAccount-Password");

                hostname = hostnameSecret.Value.Value;
                systemNumber = systemNumberSecret.Value.Value;
                client = clientSecret.Value.Value;
                username = usernameSecret.Value.Value;
                password = passwordSecret.Value.Value;

                _logger.LogInformation("SAP credentials retrieved from Key Vault");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve credentials from Key Vault, falling back to environment variables");
                
                // Fallback to environment variables
                hostname = Environment.GetEnvironmentVariable("SAP-Hostname") ?? "localhost";
                systemNumber = Environment.GetEnvironmentVariable("SAP-SystemNumber") ?? "00";
                client = Environment.GetEnvironmentVariable("SAP-Client") ?? "100";
                username = Environment.GetEnvironmentVariable("SAP-SystemAccount-Username") ?? "SAPVENDORPORTAL";
                password = Environment.GetEnvironmentVariable("SAP-SystemAccount-Password") ?? "";
            }
        }
        else
        {
            // No Key Vault client - use environment variables (local development)
            _logger.LogInformation("Key Vault not configured, using environment variables");
            
            hostname = Environment.GetEnvironmentVariable("SAP-Hostname") ?? "localhost";
            systemNumber = Environment.GetEnvironmentVariable("SAP-SystemNumber") ?? "00";
            client = Environment.GetEnvironmentVariable("SAP-Client") ?? "100";
            username = Environment.GetEnvironmentVariable("SAP-SystemAccount-Username") ?? "SAPVENDORPORTAL";
            password = Environment.GetEnvironmentVariable("SAP-SystemAccount-Password") ?? "";
        }

        _logger.LogInformation(
            "SAP Connection: Host={Host}, System={System}, Client={Client}, User={User}",
            hostname,
            systemNumber,
            client,
            username);

        // TODO: Phase 1 implementation
        // Create SAP NCo RfcDestination with basic auth
        // var destination = RfcDestinationManager.GetDestination(new RfcConfigParameters
        // {
        //     { RfcConfigParameters.AppServerHost, hostname },
        //     { RfcConfigParameters.SystemNumber, systemNumber },
        //     { RfcConfigParameters.Client, client },
        //     { RfcConfigParameters.User, username },
        //     { RfcConfigParameters.Password, password },
        //     { RfcConfigParameters.Language, "EN" }
        // });

        // For now, return mock connection
        return new MockSapConnection("BasicAuth", username);
    }

    /// <summary>
    /// Execute SAP BAPI
    /// TODO: Replace with actual SAP .NET Connector BAPI call
    /// </summary>
    private async Task<BapiResult> ExecuteBapiAsync(ISapConnection connection, SapVendorMessage message)
    {
        _logger.LogInformation("Executing BAPI_{Operation} for vendor {VendorName}", 
            message.Operation, 
            message.Vendor.Name);

        // TODO: Phase 1 implementation
        // var repository = connection.Repository;
        // var function = repository.CreateFunction("BAPI_VENDOR_CREATE");
        // 
        // // Set BAPI parameters
        // function.SetValue("VENDORNAME", message.Vendor.Name);
        // function.SetValue("TAXID", message.Vendor.TaxId);
        // // ... set other fields
        // 
        // function.Invoke(connection);
        // 
        // // Check return status
        // var returnTable = function.GetTable("RETURN");
        // if (returnTable[0].GetString("TYPE") == "E")
        // {
        //     return new BapiResult { Success = false, Errors = ... };
        // }
        // 
        // var vendorNumber = function.GetValue("VENDORNUMBER");
        // return new BapiResult { Success = true, SapVendorNumber = vendorNumber };

        // Mock implementation
        await Task.Delay(1000); // Simulate BAPI execution time

        var mockVendorNumber = $"1{DateTime.UtcNow:yyyyMMddHHmmss}";
        
        _logger.LogInformation("BAPI executed successfully. Vendor number: {VendorNumber}", mockVendorNumber);

        return new BapiResult
        {
            Success = true,
            SapVendorNumber = mockVendorNumber,
            Errors = new List<string>()
        };
    }

    /// <summary>
    /// Store vendor mapping in Cosmos DB
    /// TODO: Implement Cosmos DB client
    /// </summary>
    private async Task StoreVendorMappingAsync(string portalUserId, string sapVendorNumber)
    {
        _logger.LogInformation(
            "Storing vendor mapping: PortalUser={PortalUserId} -> SAPVendor={SapVendorNumber}",
            portalUserId,
            sapVendorNumber);

        // TODO: Phase 3 implementation
        // await cosmosDbClient.UpsertItemAsync(new VendorMapping
        // {
        //     Id = Guid.NewGuid().ToString(),
        //     PortalUserId = portalUserId,
        //     SapVendorNumber = sapVendorNumber,
        //     CreatedDate = DateTime.UtcNow,
        //     LastUpdated = DateTime.UtcNow
        // });

        await Task.CompletedTask;
    }

    /// <summary>
    /// Publish status event to Event Hubs
    /// </summary>
    private async Task PublishStatusEventAsync(SapStatusEvent statusEvent)
    {
        try
        {
            var eventData = new EventData(JsonConvert.SerializeObject(statusEvent));
            eventData.Properties.Add("CorrelationId", statusEvent.CorrelationId);
            eventData.Properties.Add("Status", statusEvent.Status);

            await _eventHubClient.SendAsync(new[] { eventData });

            _logger.LogInformation(
                "Status event published to Event Hubs. CorrelationId: {CorrelationId}, Status: {Status}",
                statusEvent.CorrelationId,
                statusEvent.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing status event to Event Hubs");
            // Don't throw - event publishing failure shouldn't fail the main operation
        }
    }
}

/// <summary>
/// Authentication method enum
/// </summary>
public enum AuthenticationMethod
{
    SystemAccount,
    IdentityPropagation
}

/// <summary>
/// SAP connection interface
/// </summary>
public interface ISapConnection
{
    string ConnectionType { get; }
    string User { get; }
}

/// <summary>
/// Mock SAP connection for Phase 1
/// TODO: Replace with actual SAP NCo connection
/// </summary>
public class MockSapConnection : ISapConnection
{
    public string ConnectionType { get; }
    public string User { get; }

    public MockSapConnection(string connectionType, string user)
    {
        ConnectionType = connectionType;
        User = user;
    }
}

/// <summary>
/// BAPI execution result
/// </summary>
public class BapiResult
{
    public bool Success { get; set; }
    public string? SapVendorNumber { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// SAP status event for Event Hubs
/// </summary>
public class SapStatusEvent
{
    public string CorrelationId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // "success" or "failure"
    public string? SapVendorNumber { get; set; }
    public List<string> Errors { get; set; } = new();
    public DateTime Timestamp { get; set; }
    public double ProcessingDuration { get; set; }
    public string? AuthMethod { get; set; }
}
