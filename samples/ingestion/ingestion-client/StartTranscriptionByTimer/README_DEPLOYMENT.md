# StartTranscriptionByTimer Azure Function - Deployment Guide

## 📋 Overview

This Azure Function processes speech transcription requests using a timer trigger. The function is deployed to `StartTranscription-20250526T161646Z` in resource group `tmsth-cci-genai-qa`.

### 🏗️ Project Structure
```
StartTranscriptionByTimer/
├── Program.cs                          # Application entry point
├── StartTranscriptionByTimer.cs        # Main function with timer trigger
├── StartTranscriptionHelper.cs         # Business logic and ServiceBus handling
├── Config/AppConfig.cs                 # Configuration class
├── Interfaces/IStartTranscriptionHelper.cs  # Interface definition
├── host.json                          # Function runtime configuration
├── local.settings.json                # Local development settings
├── function.json                      # Function metadata
├── *.csproj, *.sln                   # Project files
├── bin/                               # Build outputs
├── obj/                               # Build temporaries
├── kudu-zip/                          # Kudu deployment staging
└── kudu-deploy.zip                    # Ready-to-deploy package
```

## 🚀 Deployment Methods

### Method 1: VS Code Azure Functions Extension

**Best for:** Development and quick deployments

#### Prerequisites
1. Install Azure Functions extension in VS Code
2. Sign in to Azure account
3. Build project first

#### Steps
1. **Build the project:**
   ```powershell
   dotnet clean StartTranscriptionByTimer.csproj
   dotnet publish StartTranscriptionByTimer.csproj --configuration Release --output bin/Release/net8.0/publish
   ```

2. **Deploy via VS Code:**
   - Press `Ctrl+Shift+P`
   - Type `Azure Functions: Deploy to Function App`
   - Select `StartTranscription-20250526T161646Z`
   - Choose deployment source: `bin/Release/net8.0/publish`

#### Configuration Requirements
Create `.vscode/settings.json`:
```json
{
    "azureFunctions.deploySubpath": "bin/Release/net8.0/publish",
    "azureFunctions.projectLanguage": "C#",
    "azureFunctions.projectRuntime": "~4",
    "azureFunctions.preDeployTask": "publish (functions)"
}
```

### Method 2: Kudu Deployment

**Best for:** Production deployments and CI/CD

#### Prerequisites
1. Access to Kudu console
2. Pre-built deployment package

#### Option A: Kudu Web Interface
1. **Access Kudu Console:**
   ```
   https://StartTranscription-20250526T161646Z.scm.azurewebsites.net
   ```

2. **Upload via Drag & Drop:**
   - Navigate to `Debug console → CMD`
   - Go to `D:\home\site\wwwroot`
   - Drag `kudu-deploy.zip` to the interface
   - Kudu will extract files automatically

#### Option B: Azure CLI
```powershell
az webapp deployment source config-zip --resource-group tmsth-cci-genai-qa --name StartTranscription-20250526T161646Z --src kudu-deploy.zip
```

#### Package Structure
The `kudu-deploy.zip` contains files at root level:
```
kudu-deploy.zip
├── StartTranscriptionByTimer.dll
├── host.json
├── functions.metadata
├── extensions.json
├── worker.config.json
├── runtimes/
└── ... (all dependencies)
```

## 🔧 Build Process

### Automated Build Script
```powershell
# Clean previous builds
dotnet clean StartTranscriptionByTimer.csproj

# Build and publish
dotnet publish StartTranscriptionByTimer.csproj --configuration Release --output bin/Release/net8.0/publish

# Create Kudu deployment package
Remove-Item -Path "kudu-zip" -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path "kudu-zip" -Force
Copy-Item -Path "bin/Release/net8.0/publish/*" -Destination "kudu-zip/" -Recurse -Force
Compress-Archive -Path "kudu-zip/*" -DestinationPath "kudu-deploy.zip" -Force

Write-Host "✅ Build completed:"
Write-Host "  📁 VS Code Extension: bin/Release/net8.0/publish/"
Write-Host "  📦 Kudu Package: kudu-deploy.zip"
```

### Build Troubleshooting

**Problem:** Solution build fails with missing WorkerExtensions.csproj
```
Solution: Build individual project instead of solution
dotnet build StartTranscriptionByTimer.csproj
```

**Problem:** Large build artifacts (63+ MB)
```
Solution: Regular cleanup after deployment
Remove-Item -Path "bin", "obj", "*.zip" -Recurse -Force
```

## 📝 Changes Made (July 16, 2025)

### 🐛 Critical Bug Fix: ServiceBus Handle Leak

**Problem:** ServiceBus clients created in constructor never disposed, causing QuotaExceeded errors (4999 handle limit)

**Files Modified:**
- `StartTranscriptionHelper.cs`

**Changes:**
1. **Added IAsyncDisposable Interface:**
   ```csharp
   public class StartTranscriptionHelper : IStartTranscriptionHelper, IAsyncDisposable
   ```

2. **Implemented DisposeAsync Method:**
   ```csharp
   public async ValueTask DisposeAsync()
   {
       if (startTranscriptionReceiver != null)
           await startTranscriptionReceiver.DisposeAsync();
       if (startTranscriptionSender != null)
           await startTranscriptionSender.DisposeAsync();
       if (fetchTranscriptionSender != null)
           await fetchTranscriptionSender.DisposeAsync();
   }
   ```

3. **Enhanced Error Handling in StartTranscriptionByTimer.cs:**
   ```csharp
   catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.QuotaExceeded)
   {
       this.logger.LogError($"Service Bus QuotaExceeded: {ex.Message}");
   }
   ```

### 🏗️ Build System Improvements

**Changes:**
1. **Individual Project Building:** Use `.csproj` instead of `.sln` to avoid missing project references
2. **Dual Deployment Strategy:** Separate packages for VS Code Extension vs Kudu
3. **Automated File Management:** Organized staging areas and cleanup processes

### 📁 File Management System

**New Structure:**
- `bin/Release/net8.0/publish/` → VS Code Extension deployment
- `kudu-zip/` → Staging area for Kudu deployment
- `kudu-deploy.zip` → Ready-to-upload Kudu package

## 🔍 Monitoring & Verification

### Function App Details
- **Name:** StartTranscription-20250526T161646Z
- **Resource Group:** tmsth-cci-genai-qa
- **Runtime:** .NET 8.0 Isolated
- **Trigger:** Timer (`0 */3 * * *` - every 3 minutes)

### Health Checks
1. **Azure Portal:** Check function execution history
2. **Application Insights:** Monitor performance and errors
3. **Log Stream:** Real-time log monitoring
   ```powershell
   az webapp log tail --resource-group tmsth-cci-genai-qa --name StartTranscription-20250526T161646Z
   ```

### Common Issues & Solutions

**Issue:** QuotaExceeded ServiceBus errors
```
Solution: ✅ Fixed with IAsyncDisposable pattern
Ensures proper cleanup of ServiceBus resources
```

**Issue:** Deployment path not found
```
Solution: Build project first
dotnet publish --configuration Release --output bin/Release/net8.0/publish
```

**Issue:** Large deployment package
```
Solution: Use Release build (not Debug)
Release packages are optimized and smaller
```

## 📚 Additional Resources

### Configuration Files
- `host.json` - Azure Functions runtime configuration
- `local.settings.json` - Local development environment variables
- `function.json` - Function metadata and triggers

### Dependencies
- Microsoft.Azure.Functions.Worker
- Microsoft.Azure.Functions.Worker.Extensions.Timer
- Microsoft.Azure.Functions.Worker.Extensions.ServiceBus
- Azure.Messaging.ServiceBus

### Development Environment
- **.NET 8.0** - Target framework
- **Visual Studio Code** - IDE with Azure Functions extension
- **PowerShell** - Build and deployment scripts
- **Windows** - Development platform

---

## 🚦 Quick Start

1. **Build:** `dotnet publish StartTranscriptionByTimer.csproj --configuration Release --output bin/Release/net8.0/publish`
2. **Deploy via Extension:** Use VS Code Azure Functions extension with `bin/Release/net8.0/publish`
3. **Deploy via Kudu:** Upload `kudu-deploy.zip` to Kudu console
4. **Monitor:** Check Azure Portal for execution status

**💡 Tip:** Use the automated build script above for consistent deployments.

---
*Last updated: July 16, 2025*
*Author: มุญาฮิด ศาสนาติวงศ์ (muyahid.sassantiwong@global.ntt)*
