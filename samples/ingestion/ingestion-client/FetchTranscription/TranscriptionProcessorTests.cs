// <copyright file="TranscriptionProcessorTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
// </copyright>

namespace FetchTranscription.Tests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Azure.Messaging.ServiceBus;
    using Microsoft.Extensions.Azure;
    using Connector;
    using Connector.Database;

    [TestClass]
    public class TranscriptionProcessorTests
    {
        private Mock<IStorageConnector> mockStorageConnector;
        private Mock<IAzureClientFactory<ServiceBusClient>> mockServiceBusClientFactory;
        private Mock<ServiceBusClient> mockServiceBusClient;
        private Mock<IngestionClientDbContext> mockDatabaseContext;
        private Mock<BatchClient> mockBatchClient;
        private Mock<IOptions<AppConfig>> mockAppConfig;
        private AppConfig appConfig;
        private TranscriptionProcessor transcriptionProcessor;

        [TestInitialize]
        public void SetUp()
        {
            this.mockStorageConnector = new Mock<IStorageConnector>();
            this.mockServiceBusClientFactory = new Mock<IAzureClientFactory<ServiceBusClient>>();
            this.mockServiceBusClient = new Mock<ServiceBusClient>();
            this.mockDatabaseContext = new Mock<IngestionClientDbContext>();
            this.mockBatchClient = new Mock<BatchClient>();
            this.mockAppConfig = new Mock<IOptions<AppConfig>>();

            this.appConfig = new AppConfig
            {
                StartTranscriptionServiceBusConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test;EntityPath=start-queue",
                FetchTranscriptionServiceBusConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test;EntityPath=fetch-queue",
                CompletedServiceBusConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test;EntityPath=completed-queue"
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

            this.transcriptionProcessor = new TranscriptionProcessor(
                this.mockStorageConnector.Object,
                this.mockServiceBusClientFactory.Object,
                this.mockDatabaseContext.Object,
                this.mockBatchClient.Object,
                this.mockAppConfig.Object);
        }

        [TestMethod]
        public void Constructor_ShouldCreateServiceBusClients()
        {
            // Assert
            // Verify that ServiceBusClientFactory was called to create clients
            this.mockServiceBusClientFactory.Verify(
                f => f.CreateClient(ServiceBusClientName.StartTranscriptionServiceBusClient.ToString()),
                Times.Once);

            this.mockServiceBusClientFactory.Verify(
                f => f.CreateClient(ServiceBusClientName.FetchTranscriptionServiceBusClient.ToString()),
                Times.Once);

            this.mockServiceBusClientFactory.Verify(
                f => f.CreateClient(ServiceBusClientName.CompletedTranscriptionServiceBusClient.ToString()),
                Times.Once);
        }

        [TestMethod]
        public void Constructor_WithoutCompletedServiceBusConnectionString_ShouldNotCreateCompletedClient()
        {
            // Arrange
            var configWithoutCompleted = new AppConfig
            {
                StartTranscriptionServiceBusConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test;EntityPath=start-queue",
                FetchTranscriptionServiceBusConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test;EntityPath=fetch-queue",
                CompletedServiceBusConnectionString = null // No completed connection string
            };

            var mockConfigOptions = new Mock<IOptions<AppConfig>>();
            mockConfigOptions.Setup(x => x.Value).Returns(configWithoutCompleted);

            var mockFactory = new Mock<IAzureClientFactory<ServiceBusClient>>();
            mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(this.mockServiceBusClient.Object);

            // Act
            var processor = new TranscriptionProcessor(
                this.mockStorageConnector.Object,
                mockFactory.Object,
                this.mockDatabaseContext.Object,
                this.mockBatchClient.Object,
                mockConfigOptions.Object);

            // Assert
            mockFactory.Verify(
                f => f.CreateClient(ServiceBusClientName.StartTranscriptionServiceBusClient.ToString()),
                Times.Once);

            mockFactory.Verify(
                f => f.CreateClient(ServiceBusClientName.FetchTranscriptionServiceBusClient.ToString()),
                Times.Once);

            // Should not create completed client when connection string is null/empty
            mockFactory.Verify(
                f => f.CreateClient(ServiceBusClientName.CompletedTranscriptionServiceBusClient.ToString()),
                Times.Never);
        }

        [TestMethod]
        public async Task DisposeAsync_ShouldDisposeAllServiceBusClients()
        {
            // Arrange
            this.mockServiceBusClient
                .Setup(c => c.DisposeAsync())
                .Returns(ValueTask.CompletedTask);

            // Act
            await this.transcriptionProcessor.DisposeAsync();

            // Assert
            // Should dispose all three clients (start, fetch, completed)
            this.mockServiceBusClient.Verify(c => c.DisposeAsync(), Times.Exactly(3));
        }

        [TestMethod]
        public async Task DisposeAsync_WithNullClients_ShouldNotThrow()
        {
            // Arrange - Create processor with minimal setup that might result in null clients
            var mockFactoryReturningNull = new Mock<IAzureClientFactory<ServiceBusClient>>();
            mockFactoryReturningNull.Setup(f => f.CreateClient(It.IsAny<string>())).Returns((ServiceBusClient)null);

            var processorWithNullClients = new TranscriptionProcessor(
                this.mockStorageConnector.Object,
                mockFactoryReturningNull.Object,
                this.mockDatabaseContext.Object,
                this.mockBatchClient.Object,
                this.mockAppConfig.Object);

            // Act & Assert - Should not throw
            await processorWithNullClients.DisposeAsync();
        }

        [TestMethod]
        public async Task DisposeAsync_ShouldDisposeClientsInParallel()
        {
            // Arrange
            var disposeTaskCompletionSources = new[]
            {
                new TaskCompletionSource<bool>(),
                new TaskCompletionSource<bool>(),
                new TaskCompletionSource<bool>()
            };

            var callCount = 0;
            this.mockServiceBusClient
                .Setup(c => c.DisposeAsync())
                .Returns(() => new ValueTask(disposeTaskCompletionSources[callCount++].Task));

            // Act
            var disposeTask = this.transcriptionProcessor.DisposeAsync();

            // Assert - Task should not complete until all dispose tasks complete
            Assert.IsFalse(disposeTask.IsCompleted, "DisposeAsync should wait for all clients to dispose");

            // Complete the dispose tasks
            foreach (var tcs in disposeTaskCompletionSources)
            {
                tcs.SetResult(true);
            }

            await disposeTask;

            // Verify all clients were disposed
            this.mockServiceBusClient.Verify(c => c.DisposeAsync(), Times.Exactly(3));
        }

        [TestMethod]
        public void Constructor_WithNullServiceBusClientFactory_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
                new TranscriptionProcessor(
                    this.mockStorageConnector.Object,
                    null, // Null factory should throw
                    this.mockDatabaseContext.Object,
                    this.mockBatchClient.Object,
                    this.mockAppConfig.Object));
        }

        [TestCleanup]
        public async Task CleanUp()
        {
            if (this.transcriptionProcessor != null)
            {
                await this.transcriptionProcessor.DisposeAsync();
            }
        }
    }
}
