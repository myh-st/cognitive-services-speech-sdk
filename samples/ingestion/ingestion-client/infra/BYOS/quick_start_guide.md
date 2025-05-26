# Quick Start Guide

## 📁 Files Included

1. **`template.json`** - Main ARM template with BYOS support
2. **`parameters-byos.json`** - Parameters for BYOS scenario
3. **`parameters-regular.json`** - Parameters for regular deployment
4. **`deploy.sh`** - Bash deployment script (Linux/Mac)
5. **`deploy.ps1`** - PowerShell deployment script (Windows)
6. **`README.md`** - Detailed documentation

## 🚀 Quick Deployment Options

### Option 1: Using Azure CLI (Recommended)

#### For BYOS (Existing Storage):
```bash
# 1. Edit parameters-byos.json with your values
# 2. Deploy
az deployment group create \
  --resource-group your-rg \
  --template-file template.json \
  --parameters @parameters-byos.json
```

#### For New Storage:
```bash
# 1. Edit parameters-regular.json with your values  
# 2. Deploy
az deployment group create \
  --resource-group your-rg \
  --template-file template.json \
  --parameters @parameters-regular.json
```

### Option 2: Using Deployment Scripts

#### Bash Script (Linux/Mac):
```bash
# BYOS deployment
export TARGET_RG="my-rg"
export SPEECH_KEY="your-speech-key"
export SPEECH_REGION="westus"
export EXISTING_STORAGE_NAME="existingstorage"
export EXISTING_STORAGE_RG="speech-rg"
./deploy.sh byos

# Regular deployment
export NEW_STORAGE_NAME="newstorage123"
./deploy.sh regular
```

#### PowerShell Script (Windows):
```powershell
# BYOS deployment
.\deploy.ps1 -Scenario byos `
  -TargetResourceGroup "my-rg" `
  -SpeechKey "your-key" `
  -SpeechRegion "westus" `
  -ExistingStorageName "existingstorage" `
  -ExistingStorageResourceGroup "speech-rg"

# Regular deployment  
.\deploy.ps1 -Scenario regular `
  -TargetResourceGroup "my-rg" `
  -SpeechKey "your-key" `
  -SpeechRegion "westus" `
  -NewStorageName "newstorage123"
```

## ⚙️ Key Parameters to Configure

### Required (All Scenarios):
- **StorageAccount**: Your storage account name
- **AzureSpeechServicesKey**: Speech service subscription key
- **AzureSpeechServicesRegion**: Azure region (e.g., "westus")

### BYOS-Specific:
- **UseExistingStorageAccount**: `true`
- **ExistingStorageResourceGroup**: Resource group of existing storage
- **IsByosEnabledDeployment**: `true`

### Optional but Useful:
- **Locale**: Language for transcription (default: "en-US | English (United States)")
- **AddDiarization**: Enable speaker separation (`true`/`false`)
- **ProfanityFilterMode**: Filter profanity ("None", "Masked", "Removed", "Tags")

## 🔍 How to Choose Your Scenario

### Choose BYOS if:
- ✅ You already have Azure Speech Service with storage
- ✅ Your storage account was created by Speech Service
- ✅ You want to avoid duplicate storage costs
- ✅ Your organization uses Speech Service BYOS feature

### Choose Regular if:
- ✅ First time setting up batch transcription
- ✅ You want the template to create all resources
- ✅ You have full control over infrastructure
- ✅ You're testing or prototyping

## 📋 Pre-Deployment Checklist

- [ ] Azure CLI installed and logged in (`az login`)
- [ ] Target resource group exists
- [ ] Speech Services key and region available
- [ ] For BYOS: Existing storage account name and resource group
- [ ] For Regular: Unique storage account name chosen

## 🎯 Post-Deployment Verification

1. Check Azure Portal for created resources
2. Verify Function Apps are running
3. Test with sample audio file in input container
4. Monitor Application Insights for logs
5. Check Event Grid subscriptions are active

## 🆘 Common Issues & Solutions

| Issue | Solution |
|-------|----------|
| Storage name already exists | Choose unique name (globally unique required) |
| Permission denied | Ensure proper RBAC permissions |
| Deployment timeout | Check resource dependencies and retry |
| Function apps not starting | Verify all app settings and dependencies |

## 📞 Support Resources

- **Azure Documentation**: [Speech Service Batch Transcription](https://docs.microsoft.com/azure/cognitive-services/speech-service/batch-transcription)
- **GitHub Issues**: Create issue for template problems
- **Azure Support**: For service-level issues

---

**💡 Pro Tip**: Start with regular deployment for testing, then migrate to BYOS for production workloads to optimize costs and management.
