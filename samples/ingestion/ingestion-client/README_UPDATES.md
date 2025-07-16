# Build and Deployment Updates - July 16, 2025

## Summary of Changes

This document summarizes the updates made to support deployment from the current repository branch instead of relying on pre-built releases.

### 🎯 Objectives Achieved

1. ✅ **Modified ARM Bicep templates** to support deployment from current repository branch
2. ✅ **Created GitHub workflow** for automated build, test, and release  
3. ✅ **Fixed ServiceBus handle leak** in StartTranscriptionHelper
4. ✅ **Created build scripts** for local development and testing
5. ✅ **Comprehensive deployment guide** with multiple deployment methods

### 📁 Files Modified/Created

#### ARM Bicep Templates
- **Modified:** `infra/main.bicep`
  - Added support for multiple deployment sources (`releases`, `artifacts`, `external`)
  - Added parameters for custom repository URLs and function package URLs
  - Maintains backward compatibility with existing deployments

- **Created:** `infra/main.parameters.template.json`
  - Template parameters file for easy customization
  - Pre-configured for repository-based deployment

#### GitHub Workflow
- **Created:** `.github/workflows/build-and-release-functions.yml`
  - Builds and tests all Azure Functions on push/PR
  - Creates deployment packages (.zip files)
  - Validates Bicep templates
  - Supports manual release creation with artifacts
  - Automated deployment to Azure for specific branches

#### Build Scripts
- **Created:** `build-functions.sh` (Linux/macOS)
- **Created:** `build-functions.ps1` (Windows PowerShell)
  - Local build and packaging scripts
  - Creates deployment-ready .zip files
  - Includes tests and validation

#### Documentation
- **Created:** `DEPLOYMENT_GUIDE.md`
  - Comprehensive deployment instructions
  - Multiple deployment methods explained
  - Troubleshooting guide
  - Pre/post-deployment checklists

- **Created:** `README_UPDATES.md` (this file)
  - Summary of all changes made

### 🔧 Key Technical Changes

#### 1. ARM Bicep Template Updates

```bicep
// New parameters for flexible deployment
param DeploymentSource string = 'releases'
param RepositoryUrl string = 'https://github.com/myh-st/cognitive-services-speech-sdk'
param StartTranscriptionByTimerUrl string = ''
param FetchTranscriptionUrl string = ''
param StartTranscriptionByServiceBusUrl string = ''

// Dynamic URL generation based on deployment source
var StartTranscriptionByTimerBinary = (DeploymentSource == 'releases')
  ? '${ReleaseBinariesPrefix}${Version}/StartTranscriptionByTimer.zip'
  : (DeploymentSource == 'artifacts')
    ? '${CustomBinariesPrefix}${Version}/StartTranscriptionByTimer.zip'
    : StartTranscriptionByTimerUrl
```

#### 2. GitHub Workflow Features

- **Multi-platform build** (Ubuntu for consistency)
- **Automated testing** with test result reporting
- **Artifact management** with 30-day retention
- **Bicep validation** to catch template issues early
- **Conditional releases** via workflow dispatch
- **Environment-based deployment** with approval gates

#### 3. ServiceBus Handle Leak Fix

The critical fix addresses resource leaks in `StartTranscriptionHelper`:

```csharp
// Before: No disposal pattern
public StartTranscriptionHelper() {
    this.startTranscriptionReceiver = ...;
    this.startTranscriptionSender = ...;
    this.fetchTranscriptionSender = ...;
}

// After: Proper IAsyncDisposable implementation  
public async ValueTask DisposeAsync() {
    await this.startTranscriptionReceiver?.DisposeAsync();
    await this.startTranscriptionSender?.DisposeAsync();  
    await this.fetchTranscriptionSender?.DisposeAsync();
}
```

### 🚀 Deployment Methods

#### Method 1: GitHub Releases (Recommended)
1. Trigger workflow to create release with artifacts
2. Deploy using `DeploymentSource=artifacts` parameter

#### Method 2: External URLs
1. Build functions locally using build scripts
2. Upload to your storage/CDN
3. Deploy using `DeploymentSource=external` with custom URLs

#### Method 3: CI/CD Pipeline
1. Configure GitHub secrets and variables
2. Push to monitored branches for automatic deployment

### 🔍 Testing and Validation

#### Local Testing
```bash
# Linux/macOS
./build-functions.sh

# Windows PowerShell  
.\build-functions.ps1
```

#### Bicep Validation
```bash
az bicep build --file infra/main.bicep --stdout > /dev/null
```

#### Function Deployment Test
```bash
az functionapp deployment source config-zip \
  --resource-group your-rg \
  --name your-function-name \
  --src publish/StartTranscriptionByTimer.zip
```

### 📊 Benefits Achieved

1. **Simplified Deployment** - No dependency on upstream repository releases
2. **Faster Iteration** - Deploy directly from current branch with latest fixes  
3. **Better Control** - Multiple deployment options for different scenarios
4. **Automated Quality** - Built-in testing and validation in CI/CD
5. **Resource Efficiency** - Fixed ServiceBus handle leaks preventing quota issues
6. **Documentation** - Comprehensive guides for all deployment scenarios

### 🔮 Next Steps

1. **Test the workflow** by pushing changes to trigger builds
2. **Create first release** using workflow dispatch with release artifacts
3. **Deploy to development environment** using new parameters
4. **Monitor function performance** to validate ServiceBus fix
5. **Setup production CI/CD** with approval gates and monitoring

### 📞 Support

- **GitHub Actions Logs** - Check workflow execution details
- **Application Insights** - Monitor function runtime behavior  
- **DEPLOYMENT_GUIDE.md** - Comprehensive deployment instructions
- **GitHub Issues** - Report bugs or request features

---

**Branch:** `codex/fix-servicebusreceiver-handle-allocation-issue`  
**Repository:** `https://github.com/myh-st/cognitive-services-speech-sdk`  
**Updated:** July 16, 2025  
**Version:** 2.1.0
