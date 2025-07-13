using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using Connector;
using StartTranscriptionByTimer;

namespace StartTranscriptionByTimer.Tests
{
    [TestFixture]
    public class StartTranscriptionByTimerTests
    {
        private Mock<ILogger<StartTranscriptionByTimer>> mockLogger;
        private Mock<IStartTranscriptionHelper> mockTranscriptionHelper;
        private Mock<IAzureClientFactory<ServiceBusClient>> mockServiceBusClientFactory;
        private Mock<ServiceBusClient> mockServiceBusClient;
        private Mock<ServiceBusReceiver> mockReceiver;
        private IOptions<AppConfig> appConfig;
        private StartTranscriptionByTimer startTranscriptionByTimer;

        [SetUp]
        public void Setup()
        {
            mockLogger = new Mock<ILogger<StartTranscriptionByTimer>>();
            mockTranscriptionHelper = new Mock<IStartTranscriptionHelper>();
            mockServiceBusClientFactory = new Mock<IAzureClientFactory<ServiceBusClient>>();
            mockServiceBusClient = new Mock<ServiceBusClient>();
            mockReceiver = new Mock<ServiceBusReceiver>();

            var config = new AppConfig 
            { 
                MessagesPerFunctionExecution = 10,
                StartTranscriptionServiceBusConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test;EntityPath=test-queue"
            };
            appConfig = Options.Create(config);

            mockServiceBusClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
                                      .Returns(mockServiceBusClient.Object);

            mockServiceBusClient.Setup(c => c.CreateReceiver(It.IsAny<string>()))
                               .Returns(mockReceiver.Object);

            startTranscriptionByTimer = new StartTranscriptionByTimer(
                mockLogger.Object,
                appConfig,
                mockServiceBusClientFactory.Object,
                mockTranscriptionHelper.Object);
        }

        [Test]
        public async Task Run_NoMessages_ShouldLogAndReturn()
        {
            // Arrange
            var timerInfo = CreateTimerInfo();
            mockReceiver.Setup(r => r.ReceiveMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan>()))
                       .ReturnsAsync(new List<ServiceBusReceivedMessage>());

            // Act
            await startTranscriptionByTimer.Run(timerInfo);

            // Assert
            mockLogger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Got no messages")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Test]
        public async Task Run_ValidMessages_ShouldCompleteMessages()
        {
            // Arrange
            var timerInfo = CreateTimerInfo();
            var validMessages = CreateValidMessages(5);
            
            mockReceiver.Setup(r => r.ReceiveMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan>()))
                       .ReturnsAsync(validMessages);

            mockTranscriptionHelper.Setup(h => h.IsValidServiceBusMessage(It.IsAny<ServiceBusReceivedMessage>()))
                                  .Returns(true);

            mockReceiver.Setup(r => r.RenewMessageLockAsync(It.IsAny<ServiceBusReceivedMessage>()))
                       .Returns(Task.CompletedTask);

            mockReceiver.Setup(r => r.CompleteMessageAsync(It.IsAny<ServiceBusReceivedMessage>()))
                       .Returns(Task.CompletedTask);

            mockTranscriptionHelper.Setup(h => h.StartTranscriptionsAsync(It.IsAny<IList<ServiceBusReceivedMessage>>(), It.IsAny<DateTime>()))
                                  .Returns(Task.CompletedTask);

            // Act
            await startTranscriptionByTimer.Run(timerInfo);

            // Assert
            mockReceiver.Verify(r => r.CompleteMessageAsync(It.IsAny<ServiceBusReceivedMessage>()), 
                               Times.Exactly(5));
            mockTranscriptionHelper.Verify(h => h.StartTranscriptionsAsync(It.IsAny<IList<ServiceBusReceivedMessage>>(), It.IsAny<DateTime>()), 
                                          Times.Once);
        }

        [Test]
        public async Task Run_ProcessingFails_ShouldAbandonMessages()
        {
            // Arrange
            var timerInfo = CreateTimerInfo();
            var validMessages = CreateValidMessages(3);
            
            mockReceiver.Setup(r => r.ReceiveMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan>()))
                       .ReturnsAsync(validMessages);

            mockTranscriptionHelper.Setup(h => h.IsValidServiceBusMessage(It.IsAny<ServiceBusReceivedMessage>()))
                                  .Returns(true);

            mockReceiver.Setup(r => r.RenewMessageLockAsync(It.IsAny<ServiceBusReceivedMessage>()))
                       .Returns(Task.CompletedTask);

            mockReceiver.Setup(r => r.AbandonMessageAsync(It.IsAny<ServiceBusReceivedMessage>()))
                       .Returns(Task.CompletedTask);

            mockTranscriptionHelper.Setup(h => h.StartTranscriptionsAsync(It.IsAny<IList<ServiceBusReceivedMessage>>(), It.IsAny<DateTime>()))
                                  .ThrowsAsync(new Exception("Processing failed"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => startTranscriptionByTimer.Run(timerInfo));

            mockReceiver.Verify(r => r.AbandonMessageAsync(It.IsAny<ServiceBusReceivedMessage>()), 
                               Times.Exactly(3));
        }

        [Test]
        public async Task Run_InvalidMessages_ShouldCompleteInvalidMessages()
        {
            // Arrange
            var timerInfo = CreateTimerInfo();
            var messages = CreateValidMessages(3);
            
            mockReceiver.Setup(r => r.ReceiveMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan>()))
                       .ReturnsAsync(messages);

            mockTranscriptionHelper.Setup(h => h.IsValidServiceBusMessage(It.IsAny<ServiceBusReceivedMessage>()))
                                  .Returns(false); // All messages are invalid

            mockReceiver.Setup(r => r.CompleteMessageAsync(It.IsAny<ServiceBusReceivedMessage>()))
                       .Returns(Task.CompletedTask);

            // Act
            await startTranscriptionByTimer.Run(timerInfo);

            // Assert
            mockReceiver.Verify(r => r.CompleteMessageAsync(It.IsAny<ServiceBusReceivedMessage>()), 
                               Times.Exactly(3)); // All invalid messages completed
            mockTranscriptionHelper.Verify(h => h.StartTranscriptionsAsync(It.IsAny<IList<ServiceBusReceivedMessage>>(), It.IsAny<DateTime>()), 
                                          Times.Never); // No valid messages to process
        }

        [Test]
        public async Task Run_LockExpired_ShouldSkipExpiredMessages()
        {
            // Arrange
            var timerInfo = CreateTimerInfo();
            var expiredMessage = CreateExpiredMessage();
            var validMessage = CreateValidMessage();
            var messages = new List<ServiceBusReceivedMessage> { expiredMessage, validMessage };
            
            mockReceiver.Setup(r => r.ReceiveMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan>()))
                       .ReturnsAsync(messages);

            mockTranscriptionHelper.Setup(h => h.IsValidServiceBusMessage(It.IsAny<ServiceBusReceivedMessage>()))
                                  .Returns(true);

            mockReceiver.Setup(r => r.RenewMessageLockAsync(validMessage))
                       .Returns(Task.CompletedTask);

            mockReceiver.Setup(r => r.CompleteMessageAsync(It.IsAny<ServiceBusReceivedMessage>()))
                       .Returns(Task.CompletedTask);

            mockTranscriptionHelper.Setup(h => h.StartTranscriptionsAsync(It.IsAny<IList<ServiceBusReceivedMessage>>(), It.IsAny<DateTime>()))
                                  .Returns(Task.CompletedTask);

            // Act
            await startTranscriptionByTimer.Run(timerInfo);

            // Assert
            mockReceiver.Verify(r => r.RenewMessageLockAsync(validMessage), Times.Once);
            mockReceiver.Verify(r => r.RenewMessageLockAsync(expiredMessage), Times.Never);
            mockReceiver.Verify(r => r.CompleteMessageAsync(validMessage), Times.Once);
        }

        [Test]
        public async Task DisposeAsync_ShouldDisposeServiceBusClient()
        {
            // Arrange
            mockServiceBusClient.Setup(c => c.DisposeAsync())
                               .Returns(ValueTask.CompletedTask);

            // Act
            await startTranscriptionByTimer.DisposeAsync();

            // Assert
            mockServiceBusClient.Verify(c => c.DisposeAsync(), Times.Once);
        }

        private TimerInfo CreateTimerInfo()
        {
            return new TimerInfo
            {
                ScheduleStatus = new TimerScheduleStatus
                {
                    Next = DateTime.UtcNow.AddHours(1)
                }
            };
        }

        private List<ServiceBusReceivedMessage> CreateValidMessages(int count)
        {
            var messages = new List<ServiceBusReceivedMessage>();
            for (int i = 0; i < count; i++)
            {
                messages.Add(CreateValidMessage());
            }
            return messages;
        }

        private ServiceBusReceivedMessage CreateValidMessage()
        {
            var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
                messageId: Guid.NewGuid().ToString(),
                lockedUntil: DateTime.UtcNow.AddMinutes(5),
                body: BinaryData.FromString("test message")
            );
            return message;
        }

        private ServiceBusReceivedMessage CreateExpiredMessage()
        {
            var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
                messageId: Guid.NewGuid().ToString(),
                lockedUntil: DateTime.UtcNow.AddSeconds(-10), // Expired
                body: BinaryData.FromString("expired message")
            );
            return message;
        }
    }
}
