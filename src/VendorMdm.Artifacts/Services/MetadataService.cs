using Microsoft.Azure.Cosmos;
using VendorMdm.Shared.Models;

namespace VendorMdm.Artifacts.Services;

public interface IMetadataService
{
    // Read
    Task<IEnumerable<ReferenceDataItem>> GetReferenceDataAsync(string category);
    Task<IEnumerable<ValidationRule>> GetValidationRulesAsync(string entityType);
    Task ValidatePayloadAsync(string entityType, object payload);

    // Write (Admin)
    Task UpsertReferenceDataAsync(ReferenceDataItem item);
    Task DeleteReferenceDataAsync(string id, string category);
    Task UpsertValidationRuleAsync(ValidationRule rule);
    Task DeleteValidationRuleAsync(string id, string entityType);
}

public class MetadataService : IMetadataService
{
    private readonly Container _referenceDataContainer;
    private readonly Container _validationRulesContainer;

    public MetadataService(CosmosClient cosmosClient)
    {
        _referenceDataContainer = cosmosClient.GetContainer("VendorMdm", "ReferenceData");
        _validationRulesContainer = cosmosClient.GetContainer("VendorMdm", "ValidationRules");
    }

    public async Task<IEnumerable<ReferenceDataItem>> GetReferenceDataAsync(string category)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.category = @category AND c.isActive = true")
            .WithParameter("@category", category);

        var iterator = _referenceDataContainer.GetItemQueryIterator<ReferenceDataItem>(query);
        var results = new List<ReferenceDataItem>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }

        return results;
    }

    public async Task<IEnumerable<ValidationRule>> GetValidationRulesAsync(string entityType)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.entityType = @entityType")
            .WithParameter("@entityType", entityType);

        var iterator = _validationRulesContainer.GetItemQueryIterator<ValidationRule>(query);
        var results = new List<ValidationRule>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }

        return results;
    }

    public async Task ValidatePayloadAsync(string entityType, object payload)
    {
        var rules = await GetValidationRulesAsync(entityType);
        
        // Simple reflection-based validation example
        var properties = payload.GetType().GetProperties();
        
        foreach (var rule in rules)
        {
            var prop = properties.FirstOrDefault(p => p.Name.Equals(rule.FieldName, StringComparison.OrdinalIgnoreCase));
            if (prop != null)
            {
                var value = prop.GetValue(payload)?.ToString();

                if (rule.RuleType == "Required" && string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException(rule.ErrorMessage);
                }
                
                if (rule.RuleType == "Regex" && value != null)
                {
                    if (!System.Text.RegularExpressions.Regex.IsMatch(value, rule.RuleValue))
                    {
                        throw new ArgumentException(rule.ErrorMessage);
                    }
                }
            }
        }
    }

    public async Task UpsertReferenceDataAsync(ReferenceDataItem item)
    {
        if (string.IsNullOrEmpty(item.Id)) item.Id = Guid.NewGuid().ToString();
        await _referenceDataContainer.UpsertItemAsync(item, new PartitionKey(item.Category));
    }

    public async Task DeleteReferenceDataAsync(string id, string category)
    {
        await _referenceDataContainer.DeleteItemAsync<ReferenceDataItem>(id, new PartitionKey(category));
    }

    public async Task UpsertValidationRuleAsync(ValidationRule rule)
    {
        if (string.IsNullOrEmpty(rule.Id)) rule.Id = Guid.NewGuid().ToString();
        await _validationRulesContainer.UpsertItemAsync(rule, new PartitionKey(rule.EntityType));
    }

    public async Task DeleteValidationRuleAsync(string id, string entityType)
    {
        await _validationRulesContainer.DeleteItemAsync<ValidationRule>(id, new PartitionKey(entityType));
    }
}
