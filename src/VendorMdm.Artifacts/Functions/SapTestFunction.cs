using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.OpenApi.Models;

namespace VendorMdm.Artifacts.Functions;

/// <summary>
/// SAP Test Function - Tests SAP connectivity with simple BAPI calls
/// Use this to validate SAP connection before implementing full vendor integration
/// </summary>
public class SapTestFunction
{
    private readonly ILogger _logger;
    private readonly SecretClient? _keyVaultClient;

    public SapTestFunction(
        ILoggerFactory loggerFactory,
        SecretClient? keyVaultClient = null)
    {
        _logger = loggerFactory.CreateLogger<SapTestFunction>();
        _keyVaultClient = keyVaultClient;
    }

    /// <summary>
    /// Test SAP connectivity with RFC_PING
    /// GET /api/sap/test/ping
    /// </summary>
    [Function("SapTestPing")]
    [OpenApiOperation(operationId: "TestSapPing", tags: new[] { "SAP Test" }, Summary = "Test SAP Connectivity", Description = "Tests basic SAP connectivity using RFC_PING")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "SAP connection successful")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.InternalServerError, contentType: "application/json", bodyType: typeof(object), Description = "SAP connection failed")]
    public async Task<HttpResponseData> TestPing(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "sap/test/ping")]
        HttpRequestData req)
    {
        _logger.LogInformation("SAP Test Ping - Testing SAP connectivity");

        try
        {
            // Get SAP credentials
            var credentials = await GetSapCredentialsAsync();

            _logger.LogInformation(
                "SAP Connection Info - Host: {Host}, System: {System}, Client: {Client}, User: {User}",
                credentials.Hostname,
                credentials.SystemNumber,
                credentials.Client,
                credentials.Username);

            // TODO: Replace with actual SAP NCo call
            // var destination = CreateSapConnection(credentials);
            // var repository = destination.Repository;
            // var function = repository.CreateFunction("RFC_PING");
            // function.Invoke(destination);

            // Mock response for now
            await Task.Delay(500); // Simulate network call

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Success = true,
                Message = "SAP connection test successful (MOCK)",
                SapSystem = new
                {
                    Hostname = credentials.Hostname,
                    SystemNumber = credentials.SystemNumber,
                    Client = credentials.Client,
                    User = credentials.Username
                },
                Timestamp = DateTime.UtcNow,
                Note = "This is a mock response. Add SAP .NET Connector to test real SAP connectivity."
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing SAP connectivity");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new
            {
                Success = false,
                Error = ex.Message,
                Details = ex.ToString()
            });
            return errorResponse;
        }
    }

    /// <summary>
    /// Test SAP BAPI call - Get system info
    /// GET /api/sap/test/systeminfo
    /// </summary>
    [Function("SapTestSystemInfo")]
    [OpenApiOperation(operationId: "GetSapSystemInfo", tags: new[] { "SAP Test" }, Summary = "Get SAP System Information", Description = "Gets SAP system information using BAPI_SYSTEM_GET_INFO")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "System information retrieved successfully")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.InternalServerError, contentType: "application/json", bodyType: typeof(object), Description = "Error retrieving system information")]
    public async Task<HttpResponseData> TestSystemInfo(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "sap/test/systeminfo")]
        HttpRequestData req)
    {
        _logger.LogInformation("SAP Test System Info - Getting SAP system information");

        try
        {
            var credentials = await GetSapCredentialsAsync();

            // TODO: Replace with actual SAP NCo call
            // Call BAPI_SYSTEM_GET_INFO or similar
            // var destination = CreateSapConnection(credentials);
            // var repository = destination.Repository;
            // var function = repository.CreateFunction("BAPI_SYSTEM_GET_INFO");
            // function.Invoke(destination);
            // var systemId = function.GetValue("SYSTEMID");
            // var release = function.GetValue("RELEASE");

            // Mock response
            await Task.Delay(500);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Success = true,
                Message = "SAP system info retrieved (MOCK)",
                SystemInfo = new
                {
                    SystemId = "ECC",
                    Release = "ECC 6.0",
                    Hostname = credentials.Hostname,
                    Client = credentials.Client,
                    DatabaseSystem = "HANA",
                    OperatingSystem = "Linux"
                },
                Timestamp = DateTime.UtcNow,
                Note = "This is a mock response. Add SAP .NET Connector for real data."
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting SAP system info");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new
            {
                Success = false,
                Error = ex.Message
            });
            return errorResponse;
        }
    }

    /// <summary>
    /// Test vendor BAPI - Check if BAPI_VENDOR_CREATE is available
    /// GET /api/sap/test/vendor-bapi
    /// </summary>
    [Function("SapTestVendorBapi")]
    [OpenApiOperation(operationId: "CheckVendorBapi", tags: new[] { "SAP Test" }, Summary = "Check Vendor BAPI Availability", Description = "Checks if BAPI_VENDOR_CREATE is available in SAP system")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "BAPI availability checked successfully")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.InternalServerError, contentType: "application/json", bodyType: typeof(object), Description = "Error checking BAPI availability")]
    public async Task<HttpResponseData> TestVendorBapi(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "sap/test/vendor-bapi")]
        HttpRequestData req)
    {
        _logger.LogInformation("SAP Test Vendor BAPI - Checking BAPI_VENDOR_CREATE availability");

        try
        {
            var credentials = await GetSapCredentialsAsync();

            // TODO: Replace with actual SAP NCo call
            // Check if BAPI_VENDOR_CREATE exists
            // var destination = CreateSapConnection(credentials);
            // var repository = destination.Repository;
            // var metadata = repository.GetFunctionMetadata("BAPI_VENDOR_CREATE");

            // Mock response
            await Task.Delay(500);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Success = true,
                Message = "BAPI_VENDOR_CREATE is available (MOCK)",
                BapiInfo = new
                {
                    Name = "BAPI_VENDOR_CREATE",
                    Description = "Create Vendor Master Data",
                    Parameters = new[]
                    {
                        "VENDORNAME",
                        "TAXID",
                        "STREET",
                        "CITY",
                        "COUNTRY",
                        "POSTALCODE"
                    },
                    Available = true
                },
                Timestamp = DateTime.UtcNow,
                Note = "This is a mock response. Add SAP .NET Connector to check real BAPI metadata."
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking vendor BAPI");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new
            {
                Success = false,
                Error = ex.Message
            });
            return errorResponse;
        }
    }

    /// <summary>
    /// Test BAPI_USER_GET_DETAIL - Get user details from SAP
    /// GET /api/sap/test/user/{username}
    /// Example: /api/sap/test/user/jp_lopez
    /// </summary>
    [Function("SapTestUserDetail")]
    [OpenApiOperation(operationId: "GetSapUserDetail", tags: new[] { "SAP Test" }, Summary = "Get SAP User Details", Description = "Tests SAP connectivity by calling BAPI_USER_GET_DETAIL for a specific user")]
    [OpenApiParameter(name: "username", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "SAP username (e.g., jp_lopez)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "User details retrieved successfully")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.InternalServerError, contentType: "application/json", bodyType: typeof(object), Description = "Error retrieving user details")]
    public async Task<HttpResponseData> TestUserDetail(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "sap/test/user/{username}")]
        HttpRequestData req,
        string username)
    {
        _logger.LogInformation("SAP Test User Detail - Getting details for user: {Username}", username);

        try
        {
            var credentials = await GetSapCredentialsAsync();

            _logger.LogInformation(
                "Calling BAPI_USER_GET_DETAIL for user {Username} on SAP system {Host}",
                username,
                credentials.Hostname);

            // TODO: Replace with actual SAP NCo call
            // var destination = CreateSapConnection(credentials);
            // var repository = destination.Repository;
            // var function = repository.CreateFunction("BAPI_USER_GET_DETAIL");
            // 
            // // Set input parameter
            // function.SetValue("USERNAME", username.ToUpper());
            // 
            // // Execute BAPI
            // function.Invoke(destination);
            // 
            // // Get results
            // var addressTable = function.GetTable("ADDRESS");
            // var firstName = addressTable[0].GetString("FIRSTNAME");
            // var lastName = addressTable[0].GetString("LASTNAME");
            // var email = addressTable[0].GetString("E_MAIL");
            // 
            // // Check return messages
            // var returnTable = function.GetTable("RETURN");
            // if (returnTable.RowCount > 0 && returnTable[0].GetString("TYPE") == "E")
            // {
            //     throw new Exception(returnTable[0].GetString("MESSAGE"));
            // }

            // Mock response for now
            await Task.Delay(500);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Success = true,
                Message = $"User details retrieved for {username} (MOCK)",
                UserDetails = new
                {
                    Username = username.ToUpper(),
                    FirstName = "Juan Pablo",
                    LastName = "Lopez",
                    Email = $"{username}@company.com",
                    UserType = "A", // A = Dialog user
                    ValidFrom = "2020-01-01",
                    ValidTo = "9999-12-31",
                    LastLogon = DateTime.UtcNow.AddDays(-1),
                    UserGroup = "SUPER",
                    Department = "IT"
                },
                SapSystem = new
                {
                    Hostname = credentials.Hostname,
                    Client = credentials.Client
                },
                Timestamp = DateTime.UtcNow,
                Note = "This is a mock response. Add SAP .NET Connector to get real user data from BAPI_USER_GET_DETAIL."
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user details for {Username}", username);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new
            {
                Success = false,
                Error = ex.Message,
                Username = username
            });
            return errorResponse;
        }
    }

    /// <summary>
    /// Get SAP credentials from Key Vault or environment variables
    /// </summary>
    private async Task<SapCredentials> GetSapCredentialsAsync()
    {
        string hostname, systemNumber, client, username, password;

        // Try Key Vault first (Azure), fallback to environment variables (local)
        if (_keyVaultClient != null)
        {
            try
            {
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
                _logger.LogWarning(ex, "Failed to retrieve credentials from Key Vault, using environment variables");
                
                hostname = Environment.GetEnvironmentVariable("SAP-Hostname") ?? "PLACEHOLDER";
                systemNumber = Environment.GetEnvironmentVariable("SAP-SystemNumber") ?? "00";
                client = Environment.GetEnvironmentVariable("SAP-Client") ?? "100";
                username = Environment.GetEnvironmentVariable("SAP-SystemAccount-Username") ?? "PLACEHOLDER";
                password = Environment.GetEnvironmentVariable("SAP-SystemAccount-Password") ?? "PLACEHOLDER";
            }
        }
        else
        {
            _logger.LogInformation("Key Vault not configured, using environment variables");
            
            hostname = Environment.GetEnvironmentVariable("SAP-Hostname") ?? "PLACEHOLDER";
            systemNumber = Environment.GetEnvironmentVariable("SAP-SystemNumber") ?? "00";
            client = Environment.GetEnvironmentVariable("SAP-Client") ?? "100";
            username = Environment.GetEnvironmentVariable("SAP-SystemAccount-Username") ?? "PLACEHOLDER";
            password = Environment.GetEnvironmentVariable("SAP-SystemAccount-Password") ?? "PLACEHOLDER";
        }

        return new SapCredentials
        {
            Hostname = hostname,
            SystemNumber = systemNumber,
            Client = client,
            Username = username,
            Password = password
        };
    }
}

/// <summary>
/// SAP credentials model
/// </summary>
public class SapCredentials
{
    public string Hostname { get; set; } = string.Empty;
    public string SystemNumber { get; set; } = string.Empty;
    public string Client { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
