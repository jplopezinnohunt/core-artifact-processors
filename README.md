# Core Artifact Processors

This repository contains the **Core Platform Background Processors** - Azure Functions that handle asynchronous platform-level tasks.

## Platform Services

- **Email Service**: Sending transactional emails
- **Notification Service**: Push notifications and alerts
- **File Processing**: Document processing and transformation

## Technology Stack

- **Azure Functions v4** (.NET 8)
- **Service Bus Triggers** for event-driven processing
- **Managed Identity** for Azure resource access

## Platform Architecture

This is a **Core Platform** repository - it provides reusable background processors for multiple applications built on the platform.

## Local Development

```bash
cd src/VendorMdm.Artifacts
func start
```

## Configuration

Uses environment variables and Azure Key Vault for configuration. See `local.settings.json` for local development settings.
