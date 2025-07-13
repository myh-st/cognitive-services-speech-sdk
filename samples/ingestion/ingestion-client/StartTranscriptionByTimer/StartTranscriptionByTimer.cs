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
    public class StartTranscriptionByTimer : IAsyncDisposable
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
            this.startTranscriptionServiceBusClient = serviceBusClientFactory.CreateClient(ServiceBusClientName.StartTranscriptionServiceBusClient.ToString());
            this.startTranscriptionQueueName = ServiceBusConnectionStringProperties.Parse(this.appConfig.StartTranscriptionServiceBusConnectionString).EntityPath;
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

            this.logger.LogInformation("Pulling messages from queue...");
            await using var receiver = this.startTranscriptionServiceBusClient.CreateReceiver(this.startTranscriptionQueueName);
            var messages = await receiver.ReceiveMessagesAsync(this.appConfig.MessagesPerFunctionExecution, TimeSpan.FromSeconds(MessageReceiveTimeoutInSeconds)).ConfigureAwait(false);

            if (messages == null || !messages.Any())
            {
                this.logger.LogInformation($"Got no messages in this iteration.");
                return;
            }

            this.logger.LogInformation($"Got {messages.Count} in this iteration.");
            
            // Separate valid and invalid messages
            var messagesToProcess = new List<ServiceBusReceivedMessage>();
            var messagesToComplete = new List<ServiceBusReceivedMessage>();
            
            foreach (var message in messages)
            {
                if (message.LockedUntil > DateTime.UtcNow.AddSeconds(5))
                {
                    try
                    {
                        if (this.transcriptionHelper.IsValidServiceBusMessage(message))
                        {
                            messagesToProcess.Add(message);
                        }
                        else
                        {
                            messagesToComplete.Add(message);
                        }
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError(ex, $"Error validating message {message.MessageId}");
                        messagesToComplete.Add(message); // Complete invalid messages
                    }
                }
            }

            // Complete invalid messages
            foreach (var message in messagesToComplete)
            {
                try
                {
                    await receiver.CompleteMessageAsync(message).ConfigureAwait(false);
                }
                catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessageLockLost)
                {
                    this.logger.LogInformation($"Message lock expired for invalid message {message.MessageId}. Ignore message in this iteration.");
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, $"Failed to complete invalid message {message.MessageId}");
                }
            }

            // Renew locks for valid messages before processing
            if (messagesToProcess.Any())
            {
                var renewTasks = messagesToProcess.Select(async message =>
                {
                    try
                    {
                        await receiver.RenewMessageLockAsync(message).ConfigureAwait(false);
                        return message;
                    }
                    catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessageLockLost)
                    {
                        this.logger.LogInformation($"Message lock expired for message {message.MessageId}. Excluding from processing.");
                        return null;
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError(ex, $"Failed to renew lock for message {message.MessageId}");
                        return null;
                    }
                });

                var renewedMessages = await Task.WhenAll(renewTasks).ConfigureAwait(false);
                validServiceBusMessages.AddRange(renewedMessages.Where(m => m != null));
            }

            if (!validServiceBusMessages.Any())
            {
                this.logger.LogInformation("No valid messages were found in this function execution.");
                return;
            }

            this.logger.LogInformation($"Pulled {validServiceBusMessages.Count} valid messages from queue.");

            try
            {
                // Process the valid messages
                await this.transcriptionHelper.StartTranscriptionsAsync(validServiceBusMessages, startDateTime).ConfigureAwait(false);
                
                // Complete all successfully processed messages to remove them from the queue
                foreach (var message in validServiceBusMessages)
                {
                    try
                    {
                        await receiver.CompleteMessageAsync(message).ConfigureAwait(false);
                        this.logger.LogDebug($"Completed message with ID: {message.MessageId}");
                    }
                    catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessageLockLost)
                    {
                        this.logger.LogWarning($"Failed to complete message {message.MessageId} due to lock expiry. Message will be retried.");
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError(ex, $"Failed to complete message {message.MessageId}");
                        // Consider dead-lettering the message if completion fails repeatedly
                        try
                        {
                            await receiver.DeadLetterMessageAsync(message, "ProcessingFailed", ex.Message).ConfigureAwait(false);
                        }
                        catch (ServiceBusException dlEx)
                        {
                            this.logger.LogError(dlEx, $"Failed to dead-letter message {message.MessageId}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to process transcription messages");
                
                // Abandon all messages on processing failure so they can be retried
                foreach (var message in validServiceBusMessages)
                {
                    try
                    {
                        await receiver.AbandonMessageAsync(message).ConfigureAwait(false);
                        this.logger.LogDebug($"Abandoned message with ID: {message.MessageId}");
                    }
                    catch (ServiceBusException abandonEx) when (abandonEx.Reason == ServiceBusFailureReason.MessageLockLost)
                    {
                        this.logger.LogWarning($"Failed to abandon message {message.MessageId} due to lock expiry.");
                    }
                    catch (Exception abandonEx)
                    {
                        this.logger.LogError(abandonEx, $"Failed to abandon message {message.MessageId}");
                    }
                }
                
                throw; // Re-throw to indicate function failure
            }
        }

        /// <summary>
        /// Dispose the ServiceBusClient to prevent handle leaks.
        /// </summary>
        /// <returns>A task representing the asynchronous dispose operation.</returns>
        public async ValueTask DisposeAsync()
        {
            if (this.startTranscriptionServiceBusClient != null)
            {
                await this.startTranscriptionServiceBusClient.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
