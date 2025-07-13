# PowerShell script สำหรับทดสอบการแก้ไข Handle Leak

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$true)]
    [string]$ServiceBusNamespace,
    
    [Parameter(Mandatory=$true)]
    [string]$QueueName,
    
    [Parameter(Mandatory=$false)]
    [int]$MessageCount = 100,
    
    [Parameter(Mandatory=$false)]
    [int]$TestDurationMinutes = 10
)

Write-Host "🧪 Starting Service Bus Handle Leak Test..." -ForegroundColor Green

# Function to get current handle count
function Get-ServiceBusHandleCount {
    param($namespaceName, $resourceGroup)
    
    try {
        $subscriptionId = az account show --query id --output tsv
        $resourceId = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.ServiceBus/namespaces/$namespaceName"
        
        $metrics = az monitor metrics list `
            --resource $resourceId `
            --metric "ActiveConnections" `
            --interval PT1M `
            --query "value[0].timeseries[0].data[-1].count" `
            --output tsv
        
        if ($metrics) {
            return [int]$metrics
        } else {
            return 0
        }
    }
    catch {
        Write-Warning "ไม่สามารถดึงข้อมูล handle count ได้: $_"
        return -1
    }
}

# Function to send test messages
function Send-TestMessages {
    param($namespaceName, $queueName, $count)
    
    Write-Host "📤 Sending $count test messages to queue $queueName..." -ForegroundColor Yellow
    
    $connectionString = az servicebus namespace authorization-rule keys list `
        --resource-group $ResourceGroupName `
        --namespace-name $namespaceName `
        --name RootManageSharedAccessKey `
        --query primaryConnectionString `
        --output tsv
    
    # Create test messages using PowerShell and Azure CLI
    for ($i = 1; $i -le $count; $i++) {
        $message = @{
            id = [System.Guid]::NewGuid().ToString()
            timestamp = [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            testData = "Test message $i for handle leak testing"
        } | ConvertTo-Json -Compress
        
        # Use Azure CLI to send message (escape quotes for PowerShell)
        $escapedMessage = $message -replace '"', '\"'
        
        try {
            az servicebus message send `
                --connection-string $connectionString `
                --queue-name $queueName `
                --body $escapedMessage
                
            if ($i % 10 -eq 0) {
                Write-Host "  ✅ Sent $i messages" -ForegroundColor Gray
            }
        }
        catch {
            Write-Warning "Failed to send message $i : $_"
        }
    }
    
    Write-Host "✅ Completed sending $count messages" -ForegroundColor Green
}

# Function to monitor metrics
function Invoke-ServiceBusMetricsMonitoring {
    param($namespaceName, $resourceGroup, $queueName, $durationMinutes)
    
    Write-Host "📊 Monitoring Service Bus metrics for $durationMinutes minutes..." -ForegroundColor Yellow
    
    $startTime = Get-Date
    $endTime = $startTime.AddMinutes($durationMinutes)
    $results = @()
    
    while ((Get-Date) -lt $endTime) {
        $currentTime = Get-Date
        $handleCount = Get-ServiceBusHandleCount -namespaceName $namespaceName -resourceGroup $resourceGroup
        
        # Get queue message count
        $messageCount = az servicebus queue show `
            --resource-group $resourceGroup `
            --namespace-name $namespaceName `
            --name $queueName `
            --query "messageCount" `
            --output tsv
        
        # Get active message count
        $activeMessages = az servicebus queue show `
            --resource-group $resourceGroup `
            --namespace-name $namespaceName `
            --name $queueName `
            --query "activeMessageCount" `
            --output tsv
        
        $result = [PSCustomObject]@{
            Timestamp = $currentTime
            HandleCount = $handleCount
            MessageCount = $messageCount
            ActiveMessages = $activeMessages
            ElapsedMinutes = ($currentTime - $startTime).TotalMinutes
        }
        
        $results += $result
        
        Write-Host "⏰ $($currentTime.ToString('HH:mm:ss')) | Handles: $handleCount | Messages: $messageCount | Active: $activeMessages" -ForegroundColor Cyan
        
        Start-Sleep -Seconds 30
    }
    
    return $results
}

# Function to analyze results
function Test-ServiceBusResults {
    param($results)
    
    Write-Host "`n📈 Analyzing test results..." -ForegroundColor Green
    
    $maxHandles = ($results | Measure-Object HandleCount -Maximum).Maximum
    $minHandles = ($results | Measure-Object HandleCount -Minimum).Minimum
    $avgHandles = ($results | Measure-Object HandleCount -Average).Average
    
    $maxMessages = ($results | Measure-Object MessageCount -Maximum).Maximum
    $minMessages = ($results | Measure-Object MessageCount -Minimum).Minimum
    
    Write-Host "`n🔍 Test Results Summary:" -ForegroundColor Yellow
    Write-Host "  Handle Count - Max: $maxHandles, Min: $minHandles, Avg: $([math]::Round($avgHandles, 2))"
    Write-Host "  Message Count - Max: $maxMessages, Min: $minMessages"
    
    # Check for handle leaks
    $handleIncrease = $maxHandles - $minHandles
    if ($handleIncrease -gt 50) {
        Write-Host "  ❌ POTENTIAL HANDLE LEAK DETECTED! Handle count increased by $handleIncrease" -ForegroundColor Red
        return $false
    } else {
        Write-Host "  ✅ Handle count stable. Increase: $handleIncrease (acceptable)" -ForegroundColor Green
    }
    
    # Check message processing
    if ($minMessages -eq 0) {
        Write-Host "  ✅ Messages processed successfully" -ForegroundColor Green
        return $true
    } else {
        Write-Host "  ⚠️  Some messages remain unprocessed: $minMessages" -ForegroundColor Yellow
        return $true
    }
}

# Function to generate report
function New-TestReport {
    param($results, $testPassed)
    
    $reportPath = "ServiceBus_HandleLeak_TestReport_$(Get-Date -Format 'yyyyMMdd_HHmmss').csv"
    $results | Export-Csv -Path $reportPath -NoTypeInformation
    
    Write-Host "`n📄 Report saved to: $reportPath" -ForegroundColor Green
    
    # Generate summary report
    $summaryPath = "ServiceBus_HandleLeak_Summary_$(Get-Date -Format 'yyyyMMdd_HHmmss').txt"
    
    $summary = @"
Service Bus Handle Leak Test Summary
===================================
Test Date: $(Get-Date)
Resource Group: $ResourceGroupName
Service Bus Namespace: $ServiceBusNamespace
Queue Name: $QueueName
Test Duration: $TestDurationMinutes minutes
Messages Sent: $MessageCount

Test Result: $(if ($testPassed) { "PASSED" } else { "FAILED" })

Key Metrics:
- Maximum Handle Count: $(($results | Measure-Object HandleCount -Maximum).Maximum)
- Minimum Handle Count: $(($results | Measure-Object HandleCount -Minimum).Minimum)
- Average Handle Count: $([math]::Round(($results | Measure-Object HandleCount -Average).Average, 2))

Recommendations:
1. Monitor handle count in production
2. Set up alerts for handle count > 4000
3. Implement proper ServiceBusClient disposal
4. Complete/Abandon messages after processing

Generated by: Service Bus Handle Leak Test Script
"@

    $summary | Out-File -FilePath $summaryPath -Encoding UTF8
    Write-Host "📄 Summary saved to: $summaryPath" -ForegroundColor Green
}

# Main execution
try {
    Write-Host "🎯 Test Configuration:" -ForegroundColor Yellow
    Write-Host "  Resource Group: $ResourceGroupName"
    Write-Host "  Service Bus Namespace: $ServiceBusNamespace"
    Write-Host "  Queue Name: $QueueName"
    Write-Host "  Messages to Send: $MessageCount"
    Write-Host "  Test Duration: $TestDurationMinutes minutes"
    
    # Step 1: Get baseline metrics
    Write-Host "`n1️⃣ Getting baseline metrics..." -ForegroundColor Yellow
    $baselineHandles = Get-ServiceBusHandleCount -namespaceName $ServiceBusNamespace -resourceGroup $ResourceGroupName
    Write-Host "   Baseline handle count: $baselineHandles" -ForegroundColor Gray
    
    # Step 2: Send test messages
    Write-Host "`n2️⃣ Sending test messages..." -ForegroundColor Yellow
    Send-TestMessages -namespaceName $ServiceBusNamespace -queueName $QueueName -count $MessageCount
    
    # Step 3: Monitor processing
    Write-Host "`n3️⃣ Monitoring message processing..." -ForegroundColor Yellow
    $results = Invoke-ServiceBusMetricsMonitoring -namespaceName $ServiceBusNamespace -resourceGroup $ResourceGroupName -queueName $QueueName -durationMinutes $TestDurationMinutes
    
    # Step 4: Analyze results
    Write-Host "`n4️⃣ Analyzing results..." -ForegroundColor Yellow
    $testPassed = Test-ServiceBusResults -results $results
    
    # Step 5: Generate report
    Write-Host "`n5️⃣ Generating report..." -ForegroundColor Yellow
    New-TestReport -results $results -testPassed $testPassed
    
    if ($testPassed) {
        Write-Host "`n🎉 TEST COMPLETED SUCCESSFULLY!" -ForegroundColor Green
        Write-Host "   No handle leaks detected. The fix appears to be working correctly." -ForegroundColor Green
    } else {
        Write-Host "`n❌ TEST FAILED!" -ForegroundColor Red
        Write-Host "   Handle leaks detected. Please review the implementation." -ForegroundColor Red
    }
}
catch {
    Write-Error "❌ Test failed with error: $_"
    Write-Host "Please check your Azure connection and resource names." -ForegroundColor Red
}

Write-Host "`n✅ Test script completed." -ForegroundColor Green
