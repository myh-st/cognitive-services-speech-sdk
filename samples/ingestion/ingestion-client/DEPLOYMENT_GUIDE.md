# Azure Functions Deployment Guide for Speech Ingestion

This guide explains how to deploy the Speech Ingestion Azure Functions using the updated ARM Bicep templates and GitHub workflows.

## Overview

The project has been updated to support deploying from the current repository branch (`codex/fix-servicebusreceiver-handle-allocation-issue`) instead of relying on pre-built releases from the upstream repository.

### Key Changes Made

1. **Modified ARM Bicep Template** (`infra/main.bicep`)
   - Added support for multiple deployment sources
   - Added parameters for custom repository URLs
   - Support for GitHub releases artifacts

2. **Created GitHub Workflow** (`.github/workflows/build-and-release-functions.yml`)
   - Builds and tests all Azure Functions
   - Creates deployment packages (.zip files)
   - Validates Bicep templates
   - Supports manual release creation
   - Automated deployment to Azure

3. **Fixed ServiceBus Handle Leak**
   - Updated `StartTranscriptionHelper.cs` with proper disposal pattern
   - Prevents QuotaExceeded errors after 4999 handles

## Deployment Methods

### Method 1: Deploy from GitHub Releases (Recommended)

1. **Create a Release**
   ```bash
   # Trigger workflow manually to create release
   # Go to GitHub Actions -> "Build and Release Functions"
   # Click "Run workflow" with:
   # - create_release: true
   # - release_tag: v2.1.0
   ```

2. **Deploy Infrastructure**
   ```bash
   # Deploy using artifacts from your repository
   az deployment group create \
     --resource-group your-resource-group \
     --template-file infra/main.bicep \
     --parameters infra/main.parameters.json \
     --parameters DeploymentSource=artifacts \
     --parameters RepositoryUrl=https://github.com/myh-st/cognitive-services-speech-sdk \
     --parameters Version=v2.1.0
   ```

### Method 2: Deploy with External URLs

For more control over deployment sources:

```bash
az deployment group create \
  --resource-group your-resource-group \
  --template-file infra/main.bicep \
  --parameters DeploymentSource=external \
  --parameters StartTranscriptionByTimerUrl=https://your-storage/StartTranscriptionByTimer.zip \
  --parameters FetchTranscriptionUrl=https://your-storage/FetchTranscription.zip \
  --parameters StartTranscriptionByServiceBusUrl=https://your-storage/StartTranscriptionByServiceBus.zip
```

### Method 3: Deploy via GitHub Actions (CI/CD)

The workflow automatically deploys to Azure when code is pushed to specific branches:

1. **Setup GitHub Secrets**
   ```
   AZURE_CREDENTIALS - Service Principal credentials
   AZURE_SPEECH_KEY - Speech Services API key
   ```

2. **Setup GitHub Variables**
   ```
   AZURE_RESOURCE_GROUP - Target resource group
   AZURE_STORAGE_ACCOUNT - Storage account name
   AZURE_SPEECH_REGION - Speech Services region
   START_TRANSCRIPTION_FUNCTION_NAME - Function app name
   FETCH_TRANSCRIPTION_FUNCTION_NAME - Function app name
   ```

3. **Trigger Deployment**
   - Push to `main`, `develop`, or `codex/*` branches
   - Workflow will build, test, and deploy automatically

## Configuration Parameters

### Required Parameters

| Parameter | Description | Example |
|-----------|-------------|---------|
| `StorageAccount` | Unique storage account name | `speechingestion001` |
| `AzureSpeechServicesKey` | Speech Services API key | `your-api-key` |
| `AzureSpeechServicesRegion` | Azure region | `westus` |

### Deployment Source Parameters

| Parameter | Description | Default |
|-----------|-------------|---------|
| `DeploymentSource` | Source type: `releases`, `artifacts`, `external` | `releases` |
| `RepositoryUrl` | Repository URL for artifacts deployment | `https://github.com/myh-st/cognitive-services-speech-sdk` |
| `Version` | Release version tag | `v2.1.0` |

### Optional Parameters

| Parameter | Description | Default |
|-----------|-------------|---------|
| `Locale` | Speech recognition locale | `en-US \| English (United States)` |
| `StartTranscriptionFunctionTimeInterval` | Timer trigger interval | `0 */2 * * * *` (every 2 minutes) |
| `ProfanityFilterMode` | Profanity filtering | `None` |
| `PunctuationMode` | Punctuation mode | `Automatic` |

## Pre-Deployment Checklist

1. ✅ **Build and Test Functions**
   ```bash
   cd samples/ingestion/ingestion-client
   dotnet restore BatchIngestionClient.sln
   dotnet build BatchIngestionClient.sln --configuration Release
   dotnet test Tests/Tests.csproj --configuration Release
   ```

2. ✅ **Validate Bicep Template**
   ```bash
   az bicep build --file infra/main.bicep --stdout > /dev/null
   ```

3. ✅ **Check Resource Availability**
   ```bash
   # Verify storage account name is available
   az storage account check-name --name your-storage-account-name
   ```

4. ✅ **Prepare Parameters File**
   ```bash
   # Copy and customize parameters template
   cp infra/main.parameters.template.json infra/main.parameters.json
   # Edit with your specific values
   ```

## Post-Deployment Verification

1. **Check Function App Status**
   ```bash
   az functionapp show \
     --resource-group your-resource-group \
     --name your-function-name \
     --query "state" -o tsv
   ```

2. **View Function Logs**
   ```bash
   az webapp log tail \
     --resource-group your-resource-group \
     --name your-function-name
   ```

3. **Test Function Execution**
   ```bash
   # Trigger StartTranscriptionByTimer manually
   az functionapp function invoke \
     --resource-group your-resource-group \
     --name your-function-name \
     --function-name StartTranscriptionByTimer
   ```

## Troubleshooting

### Common Issues

1. **Build Failures**
   - Check .NET 8.0 SDK is installed
   - Verify all NuGet packages restore successfully
   - Review build logs for specific errors

2. **Deployment Failures**
   - Verify all required parameters are provided
   - Check Azure resource quotas and limits
   - Ensure proper permissions for deployment

3. **Function Runtime Issues**
   - Check Application Insights for error details
   - Verify all environment variables are configured
   - Review Service Bus connection strings

### ServiceBus Handle Leak Fix

The recent fix addresses a critical issue where ServiceBus receivers and senders were not properly disposed:

**Problem:** 
- `StartTranscriptionHelper` created ServiceBus clients in constructor
- No disposal pattern implemented  
- Led to handle exhaustion after 4999 operations

**Solution:**
- Implemented `IAsyncDisposable` pattern
- Proper cleanup of ServiceBus resources
- Prevents resource leaks and quota errors

## Next Steps

1. **Monitor Deployment**
   - Setup Application Insights alerts
   - Configure health checks
   - Monitor resource usage

2. **Setup CI/CD**
   - Configure branch protection rules
   - Setup automated testing
   - Configure deployment approvals

3. **Performance Optimization**
   - Monitor function execution times
   - Optimize batch sizes
   - Configure auto-scaling

## Support

For issues or questions:
1. Check GitHub Actions logs
2. Review Application Insights telemetry  
3. Consult Azure Functions documentation
4. Create GitHub issues for bugs or feature requests

---

**Last Updated:** July 16, 2025  
**Version:** 2.1.0  
**Branch:** codex/fix-servicebusreceiver-handle-allocation-issue
