using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using VendorMdm.Artifacts.Services;
using Newtonsoft.Json;

namespace VendorMdm.Artifacts.Functions;

public class MetadataFunction
{
    private readonly ILogger _logger;
    private readonly IMetadataService _metadataService;

    public MetadataFunction(ILoggerFactory loggerFactory, IMetadataService metadataService)
    {
        _logger = loggerFactory.CreateLogger<MetadataFunction>();
        _metadataService = metadataService;
    }

    [Function("GetReferenceData")]
    public async Task<HttpResponseData> GetReferenceData(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "metadata/reference/{category}")] HttpRequestData req,
        string category)
    {
        _logger.LogInformation($"Fetching reference data for category: {category}");
        var data = await _metadataService.GetReferenceDataAsync(category);
        
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(data);
        return response;
    }

    [Function("GetValidationRules")]
    public async Task<HttpResponseData> GetValidationRules(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "metadata/rules/{entityType}")] HttpRequestData req,
        string entityType)
    {
        _logger.LogInformation($"Fetching validation rules for entity: {entityType}");
        var data = await _metadataService.GetValidationRulesAsync(entityType);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(data);
        return response;
    }

    // --- Admin Endpoints ---

    [Function("UpsertReferenceData")]
    public async Task<HttpResponseData> UpsertReferenceData(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "metadata/reference")] HttpRequestData req)
    {
        _logger.LogInformation("Upserting reference data.");
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var item = JsonConvert.DeserializeObject<VendorMdm.Shared.Models.ReferenceDataItem>(requestBody);

        if (item == null || string.IsNullOrEmpty(item.Category))
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync("Invalid payload: Category is required.");
            return badResponse;
        }

        await _metadataService.UpsertReferenceDataAsync(item);
        
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(item);
        return response;
    }

    [Function("DeleteReferenceData")]
    public async Task<HttpResponseData> DeleteReferenceData(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "metadata/reference/{category}/{id}")] HttpRequestData req,
        string category, string id)
    {
        _logger.LogInformation($"Deleting reference data: {category}/{id}");
        try
        {
            await _metadataService.DeleteReferenceDataAsync(id, category);
            return req.CreateResponse(HttpStatusCode.OK);
        }
        catch (Microsoft.Azure.Cosmos.CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }
    }

    [Function("UpsertValidationRule")]
    public async Task<HttpResponseData> UpsertValidationRule(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "metadata/rules")] HttpRequestData req)
    {
        _logger.LogInformation("Upserting validation rule.");
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var rule = JsonConvert.DeserializeObject<VendorMdm.Shared.Models.ValidationRule>(requestBody);

        if (rule == null || string.IsNullOrEmpty(rule.EntityType))
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync("Invalid payload: EntityType is required.");
            return badResponse;
        }

        await _metadataService.UpsertValidationRuleAsync(rule);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(rule);
        return response;
    }

    [Function("DeleteValidationRule")]
    public async Task<HttpResponseData> DeleteValidationRule(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "metadata/rules/{entityType}/{id}")] HttpRequestData req,
        string entityType, string id)
    {
        _logger.LogInformation($"Deleting validation rule: {entityType}/{id}");
        try
        {
            await _metadataService.DeleteValidationRuleAsync(id, entityType);
            return req.CreateResponse(HttpStatusCode.OK);
        }
        catch (Microsoft.Azure.Cosmos.CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }
    }
}
