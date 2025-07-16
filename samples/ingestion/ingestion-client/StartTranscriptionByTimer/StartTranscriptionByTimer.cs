// <copyright file="StartTranscriptionByTimer.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
// </copyright>

namespace StartTranscriptionByTimer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;

    using Connector;
    using Connector.Enums;

    using Microsoft.Azure.Functions.Worker;
    using Microsoft.Extensions.Azure;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Start Transcription By Timer class.
    /// </summary>
    public class StartTranscriptionByTimer
    {
        private const double MessageReceiveTimeoutInSeconds = 60;

        private readonly ILogger<StartTranscriptionByTimer> logger;

        private readonly AppConfig appConfig;

        private readonly IStartTranscriptionHelper transcriptionHelper;

        private readonly ServiceBusClient startTranscriptionServiceBusClient;

        private readonly string startTranscriptionQueueName;

        /// <summary>
        /// Initializes a new instance of the <see cref="StartTranscriptionByTimer"/> class.
        /// </summary>
        /// <param name="logger">The StartTranscriptionByTimer logger</param>
        /// <param name="appConfig">environment configuration</param>
        /// <param name="serviceBusClientFactory"></param>
        /// <param name="transcriptionHelper"></param>
        public StartTranscriptionByTimer(
            ILogger<StartTranscriptionByTimer> logger,
            IOptions<AppConfig> appConfig,
            IAzureClientFactory<ServiceBusClient> serviceBusClientFactory,
            IStartTranscriptionHelper transcriptionHelper)
        {
            this.logger = logger;
            this.appConfig = appConfig?.Value;
            this.transcriptionHelper = transcriptionHelper;

            serviceBusClientFactory = serviceBusClientFactory ?? throw new ArgumentNullException(nameof(serviceBusClientFactory));
            var startTranscriptionServiceBusClient = serviceBusClientFactory.CreateClient(ServiceBusClientName.StartTranscriptionServiceBusClient.ToString());
            var startTranscriptionQueueName = ServiceBusConnectionStringProperties.Parse(this.appConfig.StartTranscriptionServiceBusConnectionString).EntityPath;
            this.startTranscriptionServiceBusClient = startTranscriptionServiceBusClient;
            this.startTranscriptionQueueName = startTranscriptionQueueName;
        }

        /// <summary>
        /// Run method to start transcription by timer.
        /// </summary>
        /// <param name="timerInfo"></param>
        /// <returns></returns>
        [Function("StartTranscriptionByTimer")]
        public async Task Run([TimerTrigger("%StartTranscriptionFunctionTimeInterval%")] TimerInfo timerInfo)
        {
            ArgumentNullException.ThrowIfNull(this.logger, nameof(this.logger));
            ArgumentNullException.ThrowIfNull(timerInfo, nameof(timerInfo));

            var startDateTime = DateTime.UtcNow;
            this.logger.LogInformation($"C# Isolated Timer trigger function v4 executed at: {startDateTime}. Next occurrence on {timerInfo.ScheduleStatus.Next}.");

            var validServiceBusMessages = new List<ServiceBusReceivedMessage>();

            try
            {
                this.logger.LogInformation("Pulling messages from queue...");
                await using var receiver = this.startTranscriptionServiceBusClient.CreateReceiver(this.startTranscriptionQueueName);
                var messages = await receiver.ReceiveMessagesAsync(this.appConfig.MessagesPerFunctionExecution, TimeSpan.FromSeconds(MessageReceiveTimeoutInSeconds)).ConfigureAwait(false);

                if (messages == null || !messages.Any())
                {
                    this.logger.LogInformation($"Got no messages in this iteration.");
                    return;
                }

                this.logger.LogInformation($"Got {messages.Count} in this iteration.");
                foreach (var message in messages)
                {
                    if (message.LockedUntil > DateTime.UtcNow.AddSeconds(5))
                    {
                        try
                        {
                            if (this.transcriptionHelper.IsValidServiceBusMessage(message))
                            {
                                await receiver.RenewMessageLockAsync(message).ConfigureAwait(false);
                                validServiceBusMessages.Add(message);
                            }
                            else
                            {
                                await receiver.CompleteMessageAsync(message).ConfigureAwait(false);
                            }
                        }
                        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessageLockLost)
                        {
                            this.logger.LogInformation($"Message lock expired for message. Ignore message in this iteration.");
                        }
                    }
                }

                if (!validServiceBusMessages.Any())
                {
                    this.logger.LogInformation("No valid messages were found in this function execution.");
                    return;
                }

                this.logger.LogInformation($"Pulled {validServiceBusMessages.Count} valid messages from queue.");

                await this.transcriptionHelper.StartTranscriptionsAsync(validServiceBusMessages, startDateTime).ConfigureAwait(false);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.QuotaExceeded)
            {
                this.logger.LogError($"Service Bus QuotaExceeded: {ex.Message}");

                // Optional: add custom alerting or retry logic here
            }
            catch (ServiceBusException ex)
            {
                this.logger.LogError($"Service Bus Exception: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                this.logger.LogError($"Unhandled Exception in Run: {ex.Message}");
                throw;
            }
        }
    }
}
