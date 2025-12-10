using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using VendorMdm.Artifacts.Data;
using VendorMdm.Shared.Models;

namespace VendorMdm.Artifacts.Services;

public interface IArtifactService
{
    // 1. Core Lifecycle: New Vendor
    Task<string> SubmitNewVendorRequestAsync(VendorApplication application, object fullPayload);

    // 2. Core Lifecycle: Modification
    Task<string> SubmitModificationRequestAsync(string sapVendorId, Guid requesterId, object fullPayload);

    // 3. Workflow: Approval
    Task ApproveRequestAsync(Guid requestId, Guid approverId);

    // 4. Sub-Entities: Attachments
    Task AddAttachmentAsync(Guid linkedEntityId, string fileName, string blobUrl);

    // 5. Read: Hybrid Data
    Task<(ChangeRequest? Request, object? Payload)> GetRequestDetailsAsync(Guid requestId);

    // 6. Read: Worklists
    Task<IEnumerable<VendorApplication>> GetPendingOnboardingRequestsAsync();
}

public class ArtifactService : IArtifactService
{
    private readonly ArtifactDbContext _sqlContext;
    private readonly Container _cosmosContainer;
    private readonly Container _eventsContainer;
    private readonly ServiceBusSender _serviceBusSender;

    public ArtifactService(
        ArtifactDbContext sqlContext, 
        CosmosClient cosmosClient, 
        ServiceBusClient serviceBusClient)
    {
        _sqlContext = sqlContext;
        _cosmosContainer = cosmosClient.GetContainer("VendorMdm", "ChangeRequestData");
        _eventsContainer = cosmosClient.GetContainer("VendorMdm", "DomainEvents");
        _serviceBusSender = serviceBusClient.CreateSender("vendor-changes");
    }

    public async Task<string> SubmitNewVendorRequestAsync(VendorApplication application, object fullPayload)
    {
        // A. Create VendorApplication (SQL)
        application.Id = Guid.NewGuid();
        application.Status = "Pending";
        application.CreatedAt = DateTime.UtcNow;
        _sqlContext.VendorApplications.Add(application);

        // B. Create Linked ChangeRequest (SQL)
        var changeRequest = new ChangeRequest
        {
            Id = Guid.NewGuid(),
            Status = "Submitted",
            RequesterId = Guid.Empty, // In real app, get from context
            CreatedAt = DateTime.UtcNow
        };
        _sqlContext.ChangeRequests.Add(changeRequest);
        
        await _sqlContext.SaveChangesAsync();

        // C. Store Payload (Cosmos)
        await SaveCosmosPayloadAsync(changeRequest.Id.ToString(), fullPayload);

        // D. Emit Event
        await EmitEventAsync("VendorApplicationSubmitted", changeRequest.Id.ToString(), new 
        { 
            ApplicationId = application.Id, 
            CompanyName = application.CompanyName 
        });

        return changeRequest.Id.ToString();
    }

    public async Task<string> SubmitModificationRequestAsync(string sapVendorId, Guid requesterId, object fullPayload)
    {
        var changeRequest = new ChangeRequest
        {
            Id = Guid.NewGuid(),
            SapVendorId = sapVendorId,
            Status = "Submitted",
            RequesterId = requesterId,
            CreatedAt = DateTime.UtcNow
        };
        _sqlContext.ChangeRequests.Add(changeRequest);
        await _sqlContext.SaveChangesAsync();

        await SaveCosmosPayloadAsync(changeRequest.Id.ToString(), fullPayload);

        await EmitEventAsync("VendorModificationSubmitted", changeRequest.Id.ToString(), new 
        { 
            SapVendorId = sapVendorId 
        });

        return changeRequest.Id.ToString();
    }

    public async Task ApproveRequestAsync(Guid requestId, Guid approverId)
    {
        var request = await _sqlContext.ChangeRequests.FindAsync(requestId);
        if (request == null) throw new KeyNotFoundException($"Request {requestId} not found");

        // Update SQL State
        request.Status = "Approved";
        request.UpdatedAt = DateTime.UtcNow;
        
        // Log the approval (could be a separate Audit table, simplifying here)
        await _sqlContext.SaveChangesAsync();

        // Emit Approval Event
        await EmitEventAsync("VendorRequestApproved", requestId.ToString(), new 
        { 
            ApproverId = approverId, 
            ApprovedAt = DateTime.UtcNow 
        });
    }

    public async Task AddAttachmentAsync(Guid linkedEntityId, string fileName, string blobUrl)
    {
        var attachment = new Attachment
        {
            Id = Guid.NewGuid(),
            LinkedEntityId = linkedEntityId,
            FileName = fileName,
            BlobUrl = blobUrl,
            UploadedAt = DateTime.UtcNow
        };

        _sqlContext.Attachments.Add(attachment);
        await _sqlContext.SaveChangesAsync();
    }

    public async Task<(ChangeRequest? Request, object? Payload)> GetRequestDetailsAsync(Guid requestId)
    {
        // Hybrid Read: SQL for Metadata, Cosmos for Payload
        var request = await _sqlContext.ChangeRequests.FindAsync(requestId);
        
        try 
        {
            ItemResponse<ChangeRequestData> response = await _cosmosContainer.ReadItemAsync<ChangeRequestData>(
                requestId.ToString(), 
                new PartitionKey(requestId.ToString())
            );
            return (request, response.Resource.Payload);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return (request, null);
        }
    }

    public async Task<IEnumerable<VendorApplication>> GetPendingOnboardingRequestsAsync()
    {
        return await _sqlContext.VendorApplications
            .Where(v => v.Status == "Pending")
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    // --- Helpers ---

    private async Task SaveCosmosPayloadAsync(string requestId, object payload)
    {
        var data = new ChangeRequestData
        {
            Id = requestId,
            RequestId = requestId,
            Payload = payload,
            NewValue = payload
        };
        await _cosmosContainer.UpsertItemAsync(data, new PartitionKey(requestId));
    }

    private async Task EmitEventAsync(string eventType, string entityId, object data)
    {
        var domainEvent = new DomainEvent
        {
            Id = Guid.NewGuid().ToString(),
            EventType = eventType,
            EntityId = entityId,
            Data = data
        };

        // 1. Event Store (Cosmos)
        await _eventsContainer.CreateItemAsync(domainEvent, new PartitionKey(eventType));

        // 2. Message Bus (Service Bus)
        var message = new ServiceBusMessage(JsonConvert.SerializeObject(domainEvent))
        {
            Subject = eventType,
            CorrelationId = entityId
        };
        await _serviceBusSender.SendMessageAsync(message);
    }
}
