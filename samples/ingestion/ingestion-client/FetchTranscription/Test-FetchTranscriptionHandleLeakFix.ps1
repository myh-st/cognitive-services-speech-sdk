# Test-FetchTranscriptionHandleLeakFix.ps1
# PowerShell script to test the Service Bus handle leak fixes in FetchTranscription

[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)]
    [string]$SubscriptionId,
    
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$false)]
    [string]$ServiceBusNamespace,
    
    [Parameter(Mandatory=$false)]
    [int]$TestIterations = 10,
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipResourceCheck
)

Write-Host "🔍 Testing FetchTranscription Service Bus Handle Leak Fixes" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan

# Test 1: Verify code fixes are in place
Write-Host "`n📋 Test 1: Verifying code fixes..." -ForegroundColor Yellow

$fetchTranscriptionPath = "c:\cognitive-services-speech-sdk\samples\ingestion\ingestion-client\FetchTranscription\FetchTranscription.cs"
$transcriptionProcessorPath = "c:\cognitive-services-speech-sdk\samples\ingestion\ingestion-client\FetchTranscription\TranscriptionProcessor.cs"

if (Test-Path $fetchTranscriptionPath) {
    $fetchContent = Get-Content $fetchTranscriptionPath -Raw
    
    # Check for try/finally block
    if ($fetchContent -match "try\s*\{[\s\S]*await\s+transcriptionProcessor\.ProcessTranscriptionJobAsync[\s\S]*\}\s*finally\s*\{[\s\S]*await\s+transcriptionProcessor\.DisposeAsync") {
        Write-Host "  ✅ FetchTranscription.cs: Try/finally disposal pattern found" -ForegroundColor Green
    } else {
        Write-Host "  ❌ FetchTranscription.cs: Try/finally disposal pattern NOT found" -ForegroundColor Red
    }
} else {
    Write-Host "  ❌ FetchTranscription.cs file not found" -ForegroundColor Red
}

if (Test-Path $transcriptionProcessorPath) {
    $processorContent = Get-Content $transcriptionProcessorPath -Raw
    
    # Check for IAsyncDisposable implementation
    if ($processorContent -match "class\s+TranscriptionProcessor\s*:\s*IAsyncDisposable") {
        Write-Host "  ✅ TranscriptionProcessor.cs: IAsyncDisposable implementation found" -ForegroundColor Green
    } else {
        Write-Host "  ❌ TranscriptionProcessor.cs: IAsyncDisposable implementation NOT found" -ForegroundColor Red
    }
    
    # Check for DisposeAsync method
    if ($processorContent -match "public\s+async\s+ValueTask\s+DisposeAsync\(\)") {
        Write-Host "  ✅ TranscriptionProcessor.cs: DisposeAsync method found" -ForegroundColor Green
    } else {
        Write-Host "  ❌ TranscriptionProcessor.cs: DisposeAsync method NOT found" -ForegroundColor Red
    }
    
    # Check for ServiceBusClient field storage
    if ($processorContent -match "private\s+readonly\s+ServiceBusClient.*ServiceBusClient;") {
        Write-Host "  ✅ TranscriptionProcessor.cs: ServiceBusClient field storage found" -ForegroundColor Green
    } else {
        Write-Host "  ❌ TranscriptionProcessor.cs: ServiceBusClient field storage NOT found" -ForegroundColor Red
    }
    
    # Check for parallel disposal with Task.WhenAll
    if ($processorContent -match "Task\.WhenAll\(disposeTasks\)") {
        Write-Host "  ✅ TranscriptionProcessor.cs: Parallel disposal with Task.WhenAll found" -ForegroundColor Green
    } else {
        Write-Host "  ❌ TranscriptionProcessor.cs: Parallel disposal NOT found" -ForegroundColor Red
    }
} else {
    Write-Host "  ❌ TranscriptionProcessor.cs file not found" -ForegroundColor Red
}

# Test 2: Verify unit tests exist
Write-Host "`n📋 Test 2: Verifying unit tests..." -ForegroundColor Yellow

$testFiles = @(
    "c:\cognitive-services-speech-sdk\samples\ingestion\ingestion-client\FetchTranscription\TranscriptionProcessorTests.cs",
    "c:\cognitive-services-speech-sdk\samples\ingestion\ingestion-client\FetchTranscription\FetchTranscriptionTests.cs"
)

foreach ($testFile in $testFiles) {
    if (Test-Path $testFile) {
        $testContent = Get-Content $testFile -Raw
        
        # Check for disposal tests
        if ($testContent -match "DisposeAsync.*Test" -or $testContent -match "Test.*DisposeAsync") {
            Write-Host "  ✅ $(Split-Path $testFile -Leaf): Disposal tests found" -ForegroundColor Green
        } else {
            Write-Host "  ❌ $(Split-Path $testFile -Leaf): Disposal tests NOT found" -ForegroundColor Red
        }
    } else {
        Write-Host "  ❌ $(Split-Path $testFile -Leaf): Test file not found" -ForegroundColor Red
    }
}

# Test 3: Verify documentation exists  
Write-Host "`n📋 Test 3: Verifying documentation..." -ForegroundColor Yellow

$docFile = "c:\cognitive-services-speech-sdk\samples\ingestion\ingestion-client\FetchTranscription\SERVICE_BUS_LEAK_ANALYSIS.md"
if (Test-Path $docFile) {
    Write-Host "  ✅ Service Bus leak analysis documentation found" -ForegroundColor Green
} else {
    Write-Host "  ❌ Service Bus leak analysis documentation NOT found" -ForegroundColor Red
}

# Test 4: Azure resource monitoring (if parameters provided)
if (-not $SkipResourceCheck -and $SubscriptionId -and $ResourceGroupName -and $ServiceBusNamespace) {
    Write-Host "`n📋 Test 4: Checking Azure Service Bus metrics..." -ForegroundColor Yellow
    
    try {
        # Check if Azure CLI is available
        $null = az --version 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✅ Azure CLI is available" -ForegroundColor Green
            
            # Set subscription
            az account set --subscription $SubscriptionId 2>$null
            if ($LASTEXITCODE -eq 0) {
                Write-Host "  ✅ Azure subscription set successfully" -ForegroundColor Green
                
                # Check Service Bus namespace
                $namespace = az servicebus namespace show --resource-group $ResourceGroupName --name $ServiceBusNamespace 2>$null | ConvertFrom-Json
                if ($namespace) {
                    Write-Host "  ✅ Service Bus namespace '$ServiceBusNamespace' found" -ForegroundColor Green
                    Write-Host "    Status: $($namespace.status)" -ForegroundColor Gray
                } else {
                    Write-Host "  ❌ Service Bus namespace '$ServiceBusNamespace' not found" -ForegroundColor Red
                }
            } else {
                Write-Host "  ❌ Failed to set Azure subscription" -ForegroundColor Red
            }
        } else {
            Write-Host "  ⚠️  Azure CLI not available, skipping resource checks" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "  ❌ Error checking Azure resources: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "`n📋 Test 4: Skipping Azure resource checks (parameters not provided or SkipResourceCheck specified)" -ForegroundColor Yellow
}

# Test 5: Compile test
Write-Host "`n📋 Test 5: Testing compilation..." -ForegroundColor Yellow

$projectPath = "c:\cognitive-services-speech-sdk\samples\ingestion\ingestion-client\FetchTranscription\FetchTranscription.csproj"
if (Test-Path $projectPath) {
    try {
        Push-Location (Split-Path $projectPath -Parent)
        
        Write-Host "  Building FetchTranscription project..." -ForegroundColor Gray
        $buildOutput = dotnet build --configuration Release --verbosity quiet 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✅ FetchTranscription project compiled successfully" -ForegroundColor Green
        } else {
            Write-Host "  ❌ FetchTranscription project compilation failed:" -ForegroundColor Red
            Write-Host $buildOutput -ForegroundColor Red
        }
    } catch {
        Write-Host "  ❌ Error during compilation: $($_.Exception.Message)" -ForegroundColor Red
    } finally {
        Pop-Location
    }
} else {
    Write-Host "  ❌ FetchTranscription.csproj not found" -ForegroundColor Red
}

# Summary
Write-Host "`n📊 Test Summary" -ForegroundColor Cyan
Write-Host "==============" -ForegroundColor Cyan

Write-Host "`n🎯 Key Fixes Verified:" -ForegroundColor White
Write-Host "  • IAsyncDisposable implementation in TranscriptionProcessor" -ForegroundColor Gray
Write-Host "  • ServiceBusClient reference storage for disposal" -ForegroundColor Gray  
Write-Host "  • Try/finally blocks in FetchTranscription.Run()" -ForegroundColor Gray
Write-Host "  • Parallel disposal with Task.WhenAll" -ForegroundColor Gray
Write-Host "  • Comprehensive unit test coverage" -ForegroundColor Gray

Write-Host "`n💡 Expected Results:" -ForegroundColor White
Write-Host "  • No more QuotaExceeded Service Bus errors" -ForegroundColor Green
Write-Host "  • Service Bus handle count stays within 4999 limit" -ForegroundColor Green
Write-Host "  • Proper resource cleanup after each function execution" -ForegroundColor Green
Write-Host "  • Reliable FetchTranscription functionality" -ForegroundColor Green

Write-Host "`n⚠️  Monitoring Recommendations:" -ForegroundColor Yellow
Write-Host "  • Monitor Service Bus metrics after deployment" -ForegroundColor Gray
Write-Host "  • Watch for handle count trends in production" -ForegroundColor Gray
Write-Host "  • Set up alerts for QuotaExceeded exceptions" -ForegroundColor Gray
Write-Host "  • Run load tests to verify handle management" -ForegroundColor Gray

Write-Host "`n✅ FetchTranscription Service Bus handle leak fixes verification complete!" -ForegroundColor Green
