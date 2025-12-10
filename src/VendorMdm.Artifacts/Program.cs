using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Azure.Cosmos;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Messaging.EventHubs.Producer;
using Microsoft.Extensions.Azure;
using VendorMdm.Artifacts.Data;
using VendorMdm.Artifacts.Services;
using VendorMdm.Shared.Services.EmailTemplates;
using VendorMdm.Shared.Models.Email;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        // Azure Credential (Managed Identity in Azure, DefaultAzureCredential for local)
        var credential = new DefaultAzureCredential();

        // 1. SQL Database
        services.AddDbContext<ArtifactDbContext>(options =>
            options.UseSqlServer(Environment.GetEnvironmentVariable("SqlConnectionString")));

        // 2. Cosmos DB
        services.AddSingleton<CosmosClient>(sp =>
        {
            var connectionString = Environment.GetEnvironmentVariable("CosmosConnectionString");
            // Use DefaultAzureCredential for production, ConnectionString for local if needed
            // For simplicity in this artifact, we assume connection string or managed identity logic here
            return new CosmosClient(connectionString); 
        });

        // 3. Service Bus
        services.AddAzureClients(clientBuilder =>
        {
            clientBuilder.AddServiceBusClient(Environment.GetEnvironmentVariable("ServiceBusConnection"));
            
            // Event Hubs for SAP status events (Phase 2)
            var eventHubNamespace = Environment.GetEnvironmentVariable("EventHubNamespace");
            if (!string.IsNullOrEmpty(eventHubNamespace))
            {
                clientBuilder.AddEventHubProducerClient(
                    eventHubNamespace, 
                    "sap-status-events")
                    .WithCredential(credential);
            }
        });

        // 4. Key Vault (for SAP credentials)
        var keyVaultUri = Environment.GetEnvironmentVariable("KeyVaultUri");
        if (!string.IsNullOrEmpty(keyVaultUri))
        {
            services.AddSingleton<SecretClient>(sp =>
            {
                return new SecretClient(new Uri(keyVaultUri), credential);
            });
        }


        // Register email templates
        services.AddScoped<IEmailTemplate<InvitationEmailData>, InvitationEmailTemplate>();

        // 5. Domain Services
        services.AddScoped<IArtifactService, ArtifactService>();
        services.AddScoped<IMetadataService, MetadataService>();
    })
    .Build();

host.Run();
