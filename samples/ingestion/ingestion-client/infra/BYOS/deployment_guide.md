# Azure Speech Batch Transcription ARM Template with BYOS Support

## Overview
This ARM template deploys Azure Speech Batch Transcription infrastructure with support for both regular deployment and BYOS (Bring Your Own Storage) scenarios.

## Key Features

### New BYOS Parameters
- **UseExistingStorageAccount**: Choose whether to use existing storage account or create new one
- **ExistingStorageResourceGroup**: Specify resource group of existing storage account
- **IsByosEnabledDeployment**: Enable BYOS mode to prevent unnecessary container creation

## Deployment Scenarios

### Scenario 1: BYOS Deployment (Using Existing Storage)
Use this when you already have a storage account created by Azure Speech Service.

**Files needed:**
- `template.json` (main ARM template)
- `parameters-byos.json` (BYOS parameters file)

**Steps:**
1. Update `parameters-byos.json` with your values:
   ```json
   {
     "StorageAccount": { "value": "your-existing-storage-name" },
     "UseExistingStorageAccount": { "value": true },
     "ExistingStorageResourceGroup": { "value": "speech-service-rg" },
     "IsByosEnabledDeployment": { "value": true },
     "AzureSpeechServicesKey": { "value": "your-speech-key" },
     "AzureSpeechServicesRegion": { "value": "westus" }
   }
   ```

2. Deploy using Azure CLI:
   ```bash
   az deployment group create \
     --resource-group your-target-rg \
     --template-file template.json \
     --parameters @parameters-byos.json
   ```

### Scenario 2: Regular Deployment (New Storage)
Use this for standard deployment where template creates all resources.

**Files needed:**
- `template.json` (main ARM template)
- `parameters-regular.json` (regular parameters file)

**Steps:**
1. Update `parameters-regular.json` with your values:
   ```json
   {
     "StorageAccount": { "value": "newstorage123" },
     "UseExistingStorageAccount": { "value": false },
     "IsByosEnabledDeployment": { "value": false },
     "AzureSpeechServicesKey": { "value": "your-speech-key" },
     "AzureSpeechServicesRegion": { "value": "westus" }
   }
   ```

2. Deploy using Azure CLI:
   ```bash
   az deployment group create \
     --resource-group your-target-rg \
     --template-file template.json \
     --parameters @parameters-regular.json
   ```

## Parameter Configuration Guide

### Required Parameters
- **StorageAccount**: Storage account name (3-24 chars, lowercase letters and numbers only)
- **AzureSpeechServicesKey**: Your Azure Speech Services subscription key
- **AzureSpeechServicesRegion**: Azure region for Speech Services

### BYOS-Specific Parameters
- **UseExistingStorageAccount**: `true` for BYOS, `false` for new storage
- **ExistingStorageResourceGroup**: Resource group of existing storage (BYOS only)
- **IsByosEnabledDeployment**: `true` to skip container creation (BYOS scenarios)

### Optional Parameters
- **CustomModelId**: Custom speech model ID (if using custom models)
- **CustomEndpoint**: Private endpoint URL (if using private endpoints)
- **TextAnalyticsKey**: For sentiment analysis and PII redaction
- **SqlAdministratorLogin**: For PowerBI integration (creates SQL database)

## Deployment Outputs
After successful deployment, you'll get:
- Function App IDs for monitoring
- Storage account name being used
- BYOS and existing storage flags for verification

## Best Practices

### For BYOS Deployments:
1. Ensure existing storage account exists and is accessible
2. Verify Speech Service has proper permissions to the storage
3. Don't modify existing containers created by Speech Service
4. Use same region for all resources when possible

### For Regular Deployments:
1. Choose unique storage account names
2. Consider using resource tags for organization
3. Monitor costs with Application Insights integration

## Troubleshooting

### Common Issues:
1. **Storage account name conflicts**: Use unique names (globally unique)
2. **Permission errors**: Ensure Service Principal has proper RBAC roles
3. **Region availability**: Some regions may not support all Speech Service features

### Validation:
- Check deployment outputs for verification
- Verify Event Grid subscriptions are created
- Test with sample audio files

## Support
For technical issues:
- Review deployment logs in Azure Portal
- Check Function App logs for runtime issues
- Verify all resource dependencies are met