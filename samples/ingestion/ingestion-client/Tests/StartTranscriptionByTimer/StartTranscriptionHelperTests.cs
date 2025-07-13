// <copyright file="StartTranscriptionHelperTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
// </copyright>

namespace Tests
{
    using System;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using Connector;
    using Microsoft.Extensions.Azure;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using StartTranscriptionByTimer;

    [TestClass]
    public class StartTranscriptionHelperTests
    {
        private Mock<ILogger<StartTranscriptionHelper>> mockLogger;
        private Mock<IStorageConnector> mockStorageConnector;
        private Mock<IAzureClientFactory<ServiceBusClient>> mockServiceBusClientFactory;
        private Mock<ServiceBusClient> mockServiceBusClient;
        private Mock<BatchClient> mockBatchClient;
        private IOptions<AppConfig> appConfigOptions;
        private StartTranscriptionHelper startTranscriptionHelper;

        public StartTranscriptionHelperTests()
        {
            this.mockLogger = new Mock<ILogger<StartTranscriptionHelper>>();
            this.mockStorageConnector = new Mock<IStorageConnector>();
            this.mockServiceBusClientFactory = new Mock<IAzureClientFactory<ServiceBusClient>>();
            this.mockServiceBusClient = new Mock<ServiceBusClient>();
            this.mockBatchClient = new Mock<BatchClient>();

            var appConfig = new AppConfig
            {
                StartTranscriptionServiceBusConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=testkey=;EntityPath=start-queue",
                FetchTranscriptionServiceBusConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=testkey=;EntityPath=fetch-queue",
                Locale = "en-US | English (United States)",
                FilesPerTranscriptionJob = 10,
                InitialPollingDelayInMinutes = 1,
                MaxPollingDelayInMinutes = 60,
                RetryLimit = 3,
                AudioInputContainer = "input",
                ErrorReportOutputContainer = "errors"
            };
            this.appConfigOptions = Options.Create(appConfig);

            this.mockServiceBusClientFactory
                .Setup(f => f.CreateClient(ServiceBusClientName.StartTranscriptionServiceBusClient.ToString()))
                .Returns(this.mockServiceBusClient.Object);

            this.mockServiceBusClientFactory
                .Setup(f => f.CreateClient(ServiceBusClientName.FetchTranscriptionServiceBusClient.ToString()))
                .Returns(this.mockServiceBusClient.Object);

            var mockReceiver = new Mock<ServiceBusReceiver>();
            var mockSender = new Mock<ServiceBusSender>();

            this.mockServiceBusClient
                .Setup(c => c.CreateReceiver(It.IsAny<string>()))
                .Returns(mockReceiver.Object);

            this.mockServiceBusClient
                .Setup(c => c.CreateSender(It.IsAny<string>()))
                .Returns(mockSender.Object);

            this.startTranscriptionHelper = new StartTranscriptionHelper(
                this.mockLogger.Object,
                this.mockStorageConnector.Object,
                this.mockServiceBusClientFactory.Object,
                this.mockBatchClient.Object,
                this.appConfigOptions);
        }

        [TestMethod]
        public async Task DisposeAsync_ShouldDisposeAllServiceBusClients()
        {
            // Arrange
            this.mockServiceBusClient
                .Setup(c => c.DisposeAsync())
                .Returns(ValueTask.CompletedTask);

            // Act
            await this.startTranscriptionHelper.DisposeAsync();

            // Assert
            // Should dispose both clients (start and fetch)
            this.mockServiceBusClient.Verify(c => c.DisposeAsync(), Times.Exactly(2));
        }

        [TestMethod]
        public void Constructor_ShouldCreateServiceBusClientsCorrectly()
        {
            // Assert
            this.mockServiceBusClientFactory.Verify(
                f => f.CreateClient(ServiceBusClientName.StartTranscriptionServiceBusClient.ToString()),
                Times.Once);

            this.mockServiceBusClientFactory.Verify(
                f => f.CreateClient(ServiceBusClientName.FetchTranscriptionServiceBusClient.ToString()),
                Times.Once);

            this.mockServiceBusClient.Verify(c => c.CreateReceiver("start-queue"), Times.Once);
            this.mockServiceBusClient.Verify(c => c.CreateSender("start-queue"), Times.Once);
            this.mockServiceBusClient.Verify(c => c.CreateSender("fetch-queue"), Times.Once);
        }

        [TestMethod]
        public void Constructor_ShouldThrowWhenServiceBusClientFactoryIsNull()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
                new StartTranscriptionHelper(
                    this.mockLogger.Object,
                    this.mockStorageConnector.Object,
                    null,
                    this.mockBatchClient.Object,
                    this.appConfigOptions));
        }
    }
}
