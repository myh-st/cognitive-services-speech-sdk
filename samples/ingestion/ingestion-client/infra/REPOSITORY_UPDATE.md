# ARM Bicep Template Update - Repository Reference Fix

## 📋 **Change Summary**
**Component**: `infra/main.bicep`  
**Purpose**: Update binary download source from Azure-Samples to current repository  
**Version**: Updated from `v2.1.12` to `v2.1.13-servicebus-leak-fix`  
**Status**: ✅ **UPDATED**

## 🔍 **Changes Made**

### **1. Repository Reference Update**
```bicep
// ❌ BEFORE - Azure-Samples (Official)
var BinariesRoutePrefix = 'https://github.com/Azure-Samples/cognitive-services-speech-sdk/releases/download/ingestion-'

// ✅ AFTER - Current Repository (With Bug Fixes)
var BinariesRoutePrefix = 'https://github.com/myh-st/cognitive-services-speech-sdk/releases/download/ingestion-'
```

### **2. Version Update**
```bicep
// ❌ BEFORE
var Version = 'v2.1.12'

// ✅ AFTER
var Version = 'v2.1.13-servicebus-leak-fix'
```

## 🎯 **Impact Analysis**

### **Binary Downloads Updated**:
| Binary | Old Source | New Source |
|--------|------------|------------|
| `StartTranscriptionByTimer.zip` | Azure-Samples | myh-st repository |
| `StartTranscriptionByServiceBus.zip` | Azure-Samples | myh-st repository |
| `FetchTranscription.zip` | Azure-Samples | myh-st repository |

### **Service Bus Fixes Included**:
- ✅ StartTranscriptionByTimer: IAsyncDisposable implementation
- ✅ FetchTranscription/TranscriptionProcessor: Resource disposal fixes
- ✅ StartTranscriptionHelper: IAsyncDisposable implementation
- ✅ Handle leak prevention for all Service Bus clients

## 🚀 **Deployment Requirements**

### **Before Deployment**:
1. **Create Release**: Tag and release version `v2.1.13-servicebus-leak-fix` in repository
2. **Build Binaries**: Create zip files for all three functions
3. **Upload Assets**: Attach binaries to GitHub release
4. **Verify URLs**: Ensure all binary URLs are accessible

### **Release Assets Required**:
```
https://github.com/myh-st/cognitive-services-speech-sdk/releases/download/ingestion-v2.1.13-servicebus-leak-fix/StartTranscriptionByTimer.zip
https://github.com/myh-st/cognitive-services-speech-sdk/releases/download/ingestion-v2.1.13-servicebus-leak-fix/StartTranscriptionByServiceBus.zip
https://github.com/myh-st/cognitive-services-speech-sdk/releases/download/ingestion-v2.1.13-servicebus-leak-fix/FetchTranscription.zip
```

## 🔧 **Configuration Impact**

### **Timer vs ServiceBus Selection**:
```bicep
var TimerBasedExecution = true  // ✅ Keeps batch processing (recommended)

WEBSITE_RUN_FROM_PACKAGE: (TimerBasedExecution
  ? StartTranscriptionByTimerBinary     // ← Uses fixed version
  : StartTranscriptionByServiceBusBinary) // ← Uses fixed version
```

### **Environment Variables Updated**:
- All Service Bus connection strings remain the same
- Functions will use improved resource management
- No breaking changes to configuration

## ✅ **Verification Steps**

1. **Repository Check**: ✅ Updated to `myh-st/cognitive-services-speech-sdk`
2. **Version Update**: ✅ Updated to `v2.1.13-servicebus-leak-fix`
3. **Binary References**: ✅ All three binaries point to new repository
4. **Service Bus Fixes**: ✅ All components include resource leak fixes

## 🎊 **Benefits**

### **Service Bus Handle Leak Prevention**:
- **StartTranscriptionByTimer**: Fixed with IAsyncDisposable
- **FetchTranscription**: Fixed TranscriptionProcessor disposal
- **StartTranscriptionHelper**: Fixed shared component disposal
- **Production Stability**: Prevents QuotaExceeded errors

### **Repository Control**:
- **Custom Fixes**: Can deploy fixes immediately
- **Version Control**: Track specific bug fixes
- **Testing**: Deploy fixes to test environments first

## 🚨 **Next Steps**

1. **Create GitHub Release**: Tag `v2.1.13-servicebus-leak-fix`
2. **Build & Package**: Create deployment packages
3. **Upload Binaries**: Attach to GitHub release
4. **Deploy Template**: Use updated Bicep template
5. **Verify Deployment**: Test Service Bus resource management

---
**Updated**: 2025-07-13  
**Repository**: Changed from Azure-Samples to myh-st  
**Version**: v2.1.13-servicebus-leak-fix  
**Status**: Ready for Release ✅
