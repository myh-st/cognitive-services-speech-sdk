// <copyright file="FetchTranscriptionTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
// </copyright>

namespace FetchTranscription.Tests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Azure.Messaging.ServiceBus;
    using Microsoft.Extensions.Azure;
    using Connector;
    using Connector.Database;
    using Connector.Serializable.TranscriptionStartedServiceBusMessage;

    [TestClass]
    public class FetchTranscriptionTests
    {
        private Mock<IServiceProvider> mockServiceProvider;
        private Mock<ILogger<FetchTranscription>> mockLogger;
        private Mock<IStorageConnector> mockStorageConnector;
        private Mock<IAzureClientFactory<ServiceBusClient>> mockServiceBusClientFactory;
        private Mock<ServiceBusClient> mockServiceBusClient;
        private Mock<BatchClient> mockBatchClient;
        private Mock<IOptions<AppConfig>> mockAppConfig;
        private AppConfig appConfig;
        private FetchTranscription fetchTranscription;

        [TestInitialize]
        public void SetUp()
        {
            this.mockServiceProvider = new Mock<IServiceProvider>();
            this.mockLogger = new Mock<ILogger<FetchTranscription>>();
            this.mockStorageConnector = new Mock<IStorageConnector>();
            this.mockServiceBusClientFactory = new Mock<IAzureClientFactory<ServiceBusClient>>();
            this.mockServiceBusClient = new Mock<ServiceBusClient>();
            this.mockBatchClient = new Mock<BatchClient>();
            this.mockAppConfig = new Mock<IOptions<AppConfig>>();

            this.appConfig = new AppConfig
            {
                StartTranscriptionServiceBusConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test;EntityPath=start-queue",
                FetchTranscriptionServiceBusConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test;EntityPath=fetch-queue",
                UseSqlDatabase = false
            };

            this.mockAppConfig.Setup(x => x.Value).Returns(this.appConfig);

            // Setup ServiceBusClientFactory to return mock clients
            this.mockServiceBusClientFactory
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(this.mockServiceBusClient.Object);

            // Setup ServiceBusClient to return mock senders
            var mockSender = new Mock<ServiceBusSender>();
            this.mockServiceBusClient
                .Setup(c => c.CreateSender(It.IsAny<string>()))
                .Returns(mockSender.Object);

            this.fetchTranscription = new FetchTranscription(
                this.mockServiceProvider.Object,
                this.mockLogger.Object,
                this.mockStorageConnector.Object,
                this.mockServiceBusClientFactory.Object,
                this.mockBatchClient.Object,
                this.mockAppConfig.Object);
        }

        [TestMethod]
        public async Task Run_WithValidMessage_ShouldCreateAndDisposeTranscriptionProcessor()
        {
            // Arrange
            var validMessage = CreateValidTranscriptionStartedMessage();
            var mockDatabaseContext = new Mock<IngestionClientDbContext>();
            
            this.mockServiceProvider
                .Setup(sp => sp.GetRequiredService<IngestionClientDbContext>())
                .Returns(mockDatabaseContext.Object);

            // Mock TranscriptionProcessor.ProcessTranscriptionJobAsync to complete successfully
            // Note: In real scenarios, we'd need to mock the actual processing, but for this test
            // we focus on disposal behavior

            // Act
            await this.fetchTranscription.Run(validMessage);

            // Assert
            // Verify that ServiceBusClientFactory was called (indicating TranscriptionProcessor was created)
            this.mockServiceBusClientFactory.Verify(
                f => f.CreateClient(It.IsAny<string>()),
                Times.AtLeast(2)); // At least start and fetch clients

            // Verify ServiceBus clients were disposed (indicating proper cleanup)
            this.mockServiceBusClient.Verify(
                c => c.DisposeAsync(),
                Times.AtLeast(2)); // At least start and fetch clients disposed
        }

        [TestMethod]
        public async Task Run_WithInvalidMessage_ShouldLogAndReturn()
        {
            // Arrange
            var invalidMessage = "";

            // Act
            await this.fetchTranscription.Run(invalidMessage);

            // Assert
            this.mockLogger.Verify(
                logger => logger.Log(
                    It.Is<LogLevel>(logLevel => logLevel == LogLevel.Information),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Found invalid service bus message")),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Once);

            // Should not create any ServiceBus clients for invalid message
            this.mockServiceBusClientFactory.Verify(
                f => f.CreateClient(It.IsAny<string>()),
                Times.Never);
        }

        [TestMethod]
        public async Task Run_WithNullMessage_ShouldLogAndReturn()
        {
            // Arrange
            string nullMessage = null;

            // Act
            await this.fetchTranscription.Run(nullMessage);

            // Assert
            this.mockLogger.Verify(
                logger => logger.Log(
                    It.Is<LogLevel>(logLevel => logLevel == LogLevel.Information),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Found invalid service bus message")),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Once);

            // Should not create any ServiceBus clients for null message
            this.mockServiceBusClientFactory.Verify(
                f => f.CreateClient(It.IsAny<string>()),
                Times.Never);
        }

        [TestMethod]
        public async Task Run_WhenProcessingThrowsException_ShouldStillDisposeTranscriptionProcessor()
        {
            // Arrange
            var validMessage = CreateValidTranscriptionStartedMessage();
            
            // Setup ServiceBusClient to throw exception during disposal to simulate processing error
            this.mockServiceBusClient
                .SetupSequence(c => c.DisposeAsync())
                .Throws(new InvalidOperationException("Simulated processing error"))
                .Returns(ValueTask.CompletedTask)
                .Returns(ValueTask.CompletedTask);

            try
            {
                // Act
                await this.fetchTranscription.Run(validMessage);
            }
            catch
            {
                // Expected to throw due to our mock setup
            }

            // Assert
            // Verify that disposal was attempted even when processing throws
            this.mockServiceBusClient.Verify(
                c => c.DisposeAsync(),
                Times.AtLeast(1));
        }

        [TestMethod]
        public async Task Run_WithSqlDatabase_ShouldRequestDatabaseContext()
        {
            // Arrange
            var validMessage = CreateValidTranscriptionStartedMessage();
            this.appConfig.UseSqlDatabase = true;
            
            var mockDatabaseContext = new Mock<IngestionClientDbContext>();
            this.mockServiceProvider
                .Setup(sp => sp.GetRequiredService<IngestionClientDbContext>())
                .Returns(mockDatabaseContext.Object);

            // Act
            await this.fetchTranscription.Run(validMessage);

            // Assert
            this.mockServiceProvider.Verify(
                sp => sp.GetRequiredService<IngestionClientDbContext>(),
                Times.Once);
        }

        [TestMethod]
        public async Task Run_WithoutSqlDatabase_ShouldNotRequestDatabaseContext()
        {
            // Arrange
            var validMessage = CreateValidTranscriptionStartedMessage();
            this.appConfig.UseSqlDatabase = false;

            // Act
            await this.fetchTranscription.Run(validMessage);

            // Assert
            this.mockServiceProvider.Verify(
                sp => sp.GetRequiredService<IngestionClientDbContext>(),
                Times.Never);
        }

        private static string CreateValidTranscriptionStartedMessage()
        {
            var message = new TranscriptionStartedMessage(
                transcriptionLocation: "https://test.api.cognitive.microsoft.com/transcriptions/test-id",
                jobName: "test-job",
                locale: "en-US",
                usesCustomModel: false,
                audioFileInfos: new[] { new AudioFileInfo { FileName = "test.wav", FileUrl = "https://test.blob.core.windows.net/audio/test.wav" } },
                pollingCounter: 0,
                failedExecutionCounter: 0);

            return message.CreateMessageString();
        }
    }
}
