using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using VendorMdm.Artifacts.Services;
using VendorMdm.Shared.Models;

namespace VendorMdm.Artifacts.Functions;

public class VendorArtifactFunction
{
    private readonly ILogger _logger;
    private readonly IArtifactService _artifactService;

    public VendorArtifactFunction(ILoggerFactory loggerFactory, IArtifactService artifactService)
    {
        _logger = loggerFactory.CreateLogger<VendorArtifactFunction>();
        _artifactService = artifactService;
    }

    [Function("SubmitVendorChange")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "vendor/changerequest")] HttpRequestData req)
    {
        _logger.LogInformation("Processing Vendor Change Request.");

        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        
        // Deserialize a composite object or just the payload
        // For simplicity, assuming the body contains the VendorApplication data
        var data = JsonConvert.DeserializeObject<VendorApplication>(requestBody);

        if (data == null)
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync("Invalid payload");
            return badResponse;
        }

        try
        {
            // In a real scenario, 'fullPayload' might be the raw JSON or a specific part of the request
            var resultId = await _artifactService.SubmitNewVendorRequestAsync(data, data);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { RequestId = resultId, Status = "Submitted" });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing request");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Internal Server Error");
            return errorResponse;
        }
    }
    [Function("GetPendingOnboardingRequests")]
    public async Task<HttpResponseData> GetPendingOnboardingRequests(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "vendor/onboarding/pending")] HttpRequestData req)
    {
        _logger.LogInformation("Fetching pending onboarding requests.");
        var requests = await _artifactService.GetPendingOnboardingRequestsAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(requests);
        return response;
    }
}
