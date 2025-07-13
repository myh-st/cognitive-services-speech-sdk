# แก้ไขปัญหา Service Bus Handle Leak

## ปัญหาที่พบ

```
Azure.Messaging.ServiceBus.ServiceBusException: Cannot allocate more handles. The maximum number of handles is 4999. (QuotaExceeded)
```

## สาเหตุหลัก

1. **ไม่ Complete valid messages**: หลังจาก process แล้วไม่ได้ Complete messages ทำให้ messages วนกลับมาซ้ำเมื่อ lock หมดอายุ
2. **ServiceBusClient ไม่ถูก Dispose**: อาจทำให้เกิด handle/memory leaks
3. **การ renew lock ไม่มีประสิทธิภาพ**: ทำทีละ message แทนการใช้ batch operations

## การแก้ไข

### 1. เพิ่ม IAsyncDisposable และ Dispose ServiceBusClient

```csharp
public class StartTranscriptionByTimer : IAsyncDisposable
{
    // ...existing code...

    public async ValueTask DisposeAsync()
    {
        if (this.startTranscriptionServiceBusClient != null)
        {
            await this.startTranscriptionServiceBusClient.DisposeAsync().ConfigureAwait(false);
        }
    }
}
```

### 2. Complete Messages หลัง Process

```csharp
try
{
    // Process messages
    await this.transcriptionHelper.StartTranscriptionsAsync(validServiceBusMessages, startDateTime);
    
    // Complete all successfully processed messages
    foreach (var message in validServiceBusMessages)
    {
        await receiver.CompleteMessageAsync(message);
    }
}
catch (Exception ex)
{
    // Abandon messages on failure for retry
    foreach (var message in validServiceBusMessages)
    {
        await receiver.AbandonMessageAsync(message);
    }
    throw;
}
```

### 3. ปรับปรุงการ Renew Lock

- แยกการ validate และ renew lock
- ใช้ `Task.WhenAll` สำหรับ parallel lock renewal
- จัดการ error handling ให้ดีขึ้น

### 4. ปรับปรุงชื่อตัวแปร

เปลี่ยนจาก:
```csharp
var startTranscriptionServiceBusClient = serviceBusClientFactory.CreateClient(...);
this.startTranscriptionServiceBusClient = startTranscriptionServiceBusClient;
```

เป็น:
```csharp
this.startTranscriptionServiceBusClient = serviceBusClientFactory.CreateClient(...);
```

## การทดสอบที่แนะนำ

### 1. Unit Tests

```csharp
[Test]
public async Task Run_ValidMessages_ShouldCompleteMessages()
{
    // Arrange
    var mockReceiver = new Mock<ServiceBusReceiver>();
    var validMessages = CreateValidMessages(10);
    
    mockReceiver.Setup(r => r.ReceiveMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan>()))
               .ReturnsAsync(validMessages);
    
    // Act
    await startTranscriptionByTimer.Run(timerInfo);
    
    // Assert
    mockReceiver.Verify(r => r.CompleteMessageAsync(It.IsAny<ServiceBusReceivedMessage>()), 
                       Times.Exactly(10));
}

[Test]
public async Task Run_ProcessingFails_ShouldAbandonMessages()
{
    // Arrange
    var mockHelper = new Mock<IStartTranscriptionHelper>();
    mockHelper.Setup(h => h.StartTranscriptionsAsync(It.IsAny<IList<ServiceBusReceivedMessage>>(), It.IsAny<DateTime>()))
              .ThrowsAsync(new Exception("Processing failed"));
    
    // Act & Assert
    await Assert.ThrowsAsync<Exception>(() => startTranscriptionByTimer.Run(timerInfo));
    
    mockReceiver.Verify(r => r.AbandonMessageAsync(It.IsAny<ServiceBusReceivedMessage>()), 
                       Times.AtLeastOnce);
}
```

### 2. Integration Tests

```csharp
[Test]
public async Task StartTranscriptionByTimer_ShouldDisposeResources()
{
    // Arrange
    var timer = new StartTranscriptionByTimer(...);
    
    // Act
    await timer.DisposeAsync();
    
    // Assert - ตรวจสอบว่า ServiceBusClient ถูก dispose
    // Monitor handle count ใน Azure Portal
}
```

### 3. Load Testing

1. สร้างข้อความจำนวนมากใน queue
2. รัน function หลายครั้งพร้อมกัน
3. Monitor handle count และ memory usage
4. ตรวจสอบว่าไม่มี handle leak

### 4. Monitoring Dashboard

```kusto
// Query สำหรับตรวจสอบ handle usage
AzureDiagnostics
| where ResourceProvider == "MICROSOFT.SERVICEBUS"
| where Category == "OperationalLogs"
| where OperationName contains "Handle"
| summarize Count = count() by bin(TimeGenerated, 5m)
| render timechart
```

## Benefits หลังการแก้ไข

1. **ไม่มี Handle Leak**: ServiceBusClient ถูก dispose อย่างถูกต้อง
2. **ไม่มี Message Duplication**: Messages ถูก complete หลัง process
3. **Better Error Handling**: Messages ถูก abandon/dead-letter ตาม case
4. **Better Performance**: Parallel lock renewal
5. **Better Monitoring**: เพิ่ม detailed logging

## การ Deploy

1. ทดสอบใน Development environment ก่อน
2. Monitor handle count และ queue message count
3. Deploy ไป Staging
4. ทดสอบ load และ monitor metrics
5. Deploy ไป Production พร้อม alerts

## Monitoring Metrics ที่ควรติดตาม

- Service Bus Active Connections
- Service Bus Handle Count
- Queue Message Count
- Function Execution Time
- Function Success/Failure Rate
- Memory Usage
