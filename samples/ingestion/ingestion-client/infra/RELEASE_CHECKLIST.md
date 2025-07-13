# Service Bus Leak Fix - Release Checklist

## 📋 **Release Overview**
**Version**: `v2.1.13-servicebus-leak-fix`  
**Repository**: `myh-st/cognitive-services-speech-sdk`  
**Purpose**: Deploy Service Bus resource leak fixes to production

## ✅ **Pre-Release Checklist**

### **1. Code Changes Verification**
- [x] ✅ StartTranscriptionByTimer: IAsyncDisposable implemented
- [x] ✅ StartTranscriptionHelper: IAsyncDisposable implemented  
- [x] ✅ TranscriptionProcessor: IAsyncDisposable implemented
- [x] ✅ Unit tests created and validated
- [x] ✅ ARM Bicep template updated to use current repository

### **2. Repository Updates**
- [x] ✅ Bicep template points to `myh-st/cognitive-services-speech-sdk`
- [x] ✅ Version updated to `v2.1.13-servicebus-leak-fix`
- [x] ✅ Documentation created (SERVICE_BUS_LEAK_FIX.md, REPOSITORY_UPDATE.md)

### **3. Git Operations**
```bash
# Commit all changes
git add .
git commit -m "Fix Service Bus resource leaks - v2.1.13-servicebus-leak-fix

- Add IAsyncDisposable to StartTranscriptionByTimer
- Add IAsyncDisposable to StartTranscriptionHelper  
- Add IAsyncDisposable to TranscriptionProcessor
- Update ARM Bicep template to use current repository
- Add comprehensive unit tests and documentation"

# Create and push tag
git tag v2.1.13-servicebus-leak-fix
git push origin master
git push origin v2.1.13-servicebus-leak-fix
```

## 🔧 **Build and Package**

### **1. Build Commands**
```powershell
# StartTranscriptionByTimer
cd "c:\cognitive-services-speech-sdk\samples\ingestion\ingestion-client\StartTranscriptionByTimer"
dotnet publish -c Release -o bin\Release\publish
Compress-Archive -Path bin\Release\publish\* -DestinationPath StartTranscriptionByTimer.zip

# StartTranscriptionByServiceBus  
cd "c:\cognitive-services-speech-sdk\samples\ingestion\ingestion-client\StartTranscriptionByServiceBus"
dotnet publish -c Release -o bin\Release\publish
Compress-Archive -Path bin\Release\publish\* -DestinationPath StartTranscriptionByServiceBus.zip

# FetchTranscription
cd "c:\cognitive-services-speech-sdk\samples\ingestion\ingestion-client\FetchTranscription"
dotnet publish -c Release -o bin\Release\publish
Compress-Archive -Path bin\Release\publish\* -DestinationPath FetchTranscription.zip
```

### **2. Required Binary Assets**
- [ ] `StartTranscriptionByTimer.zip`
- [ ] `StartTranscriptionByServiceBus.zip`  
- [ ] `FetchTranscription.zip`

## 🚀 **GitHub Release Process**

### **1. Create Release**
1. Go to: https://github.com/myh-st/cognitive-services-speech-sdk/releases
2. Click "Create a new release"
3. Choose tag: `v2.1.13-servicebus-leak-fix`
4. Release title: `Service Bus Resource Leak Fixes - v2.1.13`

### **2. Release Description**
```markdown
# Service Bus Resource Leak Fixes - v2.1.13

## 🐛 **Bugs Fixed**
- **Service Bus Handle Leak**: Fixed QuotaExceeded error (4999 handle limit)
- **Resource Disposal**: Added IAsyncDisposable pattern to all Service Bus components
- **Memory Management**: Proper cleanup of ServiceBusClient instances

## 🔧 **Components Updated**
- ✅ **StartTranscriptionByTimer**: Added IAsyncDisposable with parallel disposal
- ✅ **StartTranscriptionHelper**: Added IAsyncDisposable for shared Service Bus clients  
- ✅ **TranscriptionProcessor**: Added IAsyncDisposable with resource cleanup
- ✅ **ARM Bicep Template**: Updated to use current repository for deployments

## 🧪 **Testing**
- ✅ Unit tests added for all disposal scenarios
- ✅ Service Bus client creation and disposal verified
- ✅ Parallel disposal performance optimized

## 📦 **Deployment Assets**
This release includes the following binary packages:
- `StartTranscriptionByTimer.zip` - Timer-based batch processing (recommended)
- `StartTranscriptionByServiceBus.zip` - Service Bus triggered processing
- `FetchTranscription.zip` - Transcription result fetching

## 🎯 **Breaking Changes**
- **None**: All changes are backward compatible
- **Configuration**: No changes required to existing deployments
- **API**: All public interfaces remain unchanged

## 📋 **Deployment Instructions**
1. Use the updated ARM Bicep template from `infra/main.bicep`
2. Template automatically uses fixed binaries from this release
3. No configuration changes required for existing deployments
```

### **3. Upload Assets**
- [ ] Upload `StartTranscriptionByTimer.zip`
- [ ] Upload `StartTranscriptionByServiceBus.zip`
- [ ] Upload `FetchTranscription.zip`

### **4. Verify URLs**
Expected URLs after release:
- `https://github.com/myh-st/cognitive-services-speech-sdk/releases/download/ingestion-v2.1.13-servicebus-leak-fix/StartTranscriptionByTimer.zip`
- `https://github.com/myh-st/cognitive-services-speech-sdk/releases/download/ingestion-v2.1.13-servicebus-leak-fix/StartTranscriptionByServiceBus.zip`
- `https://github.com/myh-st/cognitive-services-speech-sdk/releases/download/ingestion-v2.1.13-servicebus-leak-fix/FetchTranscription.zip`

## 🎊 **Post-Release Verification**

### **1. ARM Template Deployment**
- [ ] Deploy using updated Bicep template
- [ ] Verify binary downloads work correctly
- [ ] Check Function Apps start successfully

### **2. Service Bus Resource Monitoring**
- [ ] Monitor Service Bus handle usage
- [ ] Verify no QuotaExceeded errors
- [ ] Check Function App logs for disposal confirmation

### **3. Production Validation**
- [ ] Test transcription job processing
- [ ] Verify Service Bus message handling
- [ ] Monitor application performance metrics

---
**Release Manager**: Generated by Service Bus Leak Fix automation  
**Date**: 2025-07-13  
**Status**: Ready for Release 🚀
