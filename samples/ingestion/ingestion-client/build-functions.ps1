# Build and Package Azure Functions for Speech Ingestion
# PowerShell script for Windows environments

param(
    [string]$Configuration = "Release",
    [string]$OutputDirectory = "publish",
    [switch]$SkipTests,
    [switch]$Help
)

if ($Help) {
    Write-Host @"
Build and Package Azure Functions for Speech Ingestion

USAGE:
    .\build-functions.ps1 [OPTIONS]

OPTIONS:
    -Configuration <config>    Build configuration (Release/Debug) [default: Release]
    -OutputDirectory <dir>     Output directory for packages [default: publish]  
    -SkipTests                 Skip running unit tests
    -Help                      Show this help message

EXAMPLES:
    .\build-functions.ps1
    .\build-functions.ps1 -Configuration Debug -SkipTests
    .\build-functions.ps1 -OutputDirectory "dist"
"@
    exit 0
}

# Configuration
$SolutionFile = "BatchIngestionClient.sln"
$ErrorActionPreference = "Stop"

Write-Host "🚀 Starting build process for Speech Ingestion Functions" -ForegroundColor Green

# Check if .NET is installed
try {
    $dotnetVersion = dotnet --version
    Write-Host "✅ .NET SDK version: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ .NET SDK not found. Please install .NET 8.0 SDK" -ForegroundColor Red
    exit 1
}

# Clean previous builds
Write-Host "🧹 Cleaning previous builds" -ForegroundColor Yellow
if (Test-Path $OutputDirectory) {
    Remove-Item -Path $OutputDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

# Restore dependencies
Write-Host "📦 Restoring dependencies" -ForegroundColor Yellow
dotnet restore $SolutionFile

# Build solution
Write-Host "🔨 Building solution" -ForegroundColor Yellow
dotnet build $SolutionFile --configuration $Configuration --no-restore

# Run tests (if not skipped)
if (-not $SkipTests) {
    Write-Host "🧪 Running tests" -ForegroundColor Yellow
    dotnet test Tests/Tests.csproj --configuration $Configuration --no-build --verbosity normal
} else {
    Write-Host "⏭️  Skipping tests" -ForegroundColor Yellow
}

# Function projects to build
$FunctionProjects = @(
    @{ Name = "StartTranscriptionByTimer"; Path = "StartTranscriptionByTimer/StartTranscriptionByTimer.csproj" },
    @{ Name = "FetchTranscription"; Path = "FetchTranscription/FetchTranscription.csproj" },
    @{ Name = "StartTranscriptionByServiceBus"; Path = "StartTranscriptionByServiceBus/StartTranscriptionByServiceBus.csproj" }
)

# Publish individual functions
foreach ($project in $FunctionProjects) {
    Write-Host "📦 Publishing $($project.Name) function" -ForegroundColor Yellow
    $outputPath = Join-Path $OutputDirectory $project.Name
    
    dotnet publish $project.Path `
        --configuration $Configuration `
        --output $outputPath `
        --no-restore
}

# Create zip packages
Write-Host "📦 Creating deployment packages" -ForegroundColor Yellow

foreach ($project in $FunctionProjects) {
    $folderPath = Join-Path $OutputDirectory $project.Name
    $zipPath = Join-Path $OutputDirectory "$($project.Name).zip"
    
    Write-Host "  📄 Creating $($project.Name).zip" -ForegroundColor White
    
    # Use .NET compression if available, otherwise use PowerShell 5+ Compress-Archive
    if (Get-Command "Compress-Archive" -ErrorAction SilentlyContinue) {
        Compress-Archive -Path "$folderPath\*" -DestinationPath $zipPath -Force
    } else {
        # Fallback for older PowerShell versions
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [System.IO.Compression.ZipFile]::CreateFromDirectory($folderPath, $zipPath)
    }
}

# Verify packages
Write-Host "✅ Deployment packages created successfully:" -ForegroundColor Green
Get-ChildItem -Path $OutputDirectory -Filter "*.zip" | ForEach-Object {
    $sizeKB = [math]::Round($_.Length / 1KB, 2)
    $sizeMB = [math]::Round($_.Length / 1MB, 2)
    
    if ($sizeMB -ge 1) {
        Write-Host "  📦 $($_.Name): $sizeMB MB" -ForegroundColor White
    } else {
        Write-Host "  📦 $($_.Name): $sizeKB KB" -ForegroundColor White
    }
}

Write-Host "🎉 Build process completed successfully!" -ForegroundColor Green
Write-Host "📁 Deployment packages are available in: $OutputDirectory" -ForegroundColor Green
Write-Host "🚀 Ready for deployment to Azure Functions" -ForegroundColor Green

# Display next steps
Write-Host ""
Write-Host "📋 Next Steps:" -ForegroundColor Yellow
Write-Host "  1. Review the deployment packages in $OutputDirectory/" -ForegroundColor White
Write-Host "  2. Upload to Azure Functions using Azure CLI or Azure Portal" -ForegroundColor White  
Write-Host "  3. Update Bicep template with custom URLs if needed" -ForegroundColor White
Write-Host "  4. Deploy infrastructure using: az deployment group create ..." -ForegroundColor White
Write-Host ""
Write-Host "💡 For detailed deployment instructions, see DEPLOYMENT_GUIDE.md" -ForegroundColor Green

# Show command to deploy
Write-Host ""
Write-Host "🚀 Example deployment command:" -ForegroundColor Cyan
Write-Host @"
az deployment group create \
  --resource-group your-resource-group \
  --template-file infra/main.bicep \
  --parameters infra/main.parameters.json \
  --parameters DeploymentSource=external \
  --parameters StartTranscriptionByTimerUrl=https://your-storage/StartTranscriptionByTimer.zip \
  --parameters FetchTranscriptionUrl=https://your-storage/FetchTranscription.zip \
  --parameters StartTranscriptionByServiceBusUrl=https://your-storage/StartTranscriptionByServiceBus.zip
"@ -ForegroundColor DarkCyan
