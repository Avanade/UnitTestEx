// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Azure.Messaging.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Mime;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.Assertors;

namespace UnitTestEx.Functions
{
    /// <summary>
    /// Provides Azure Function <see cref="ServiceBusTriggerAttribute"/> emulator for integration testing: <see cref="ClearAsync"/>, <see cref="SendAsync(ServiceBusMessage[])"/> and <see cref="RunAsync(TimeSpan?, bool?)"/> capabilities.
    /// </summary>
    /// <typeparam name="TFunction">The Azure Function <see cref="Type"/>.</typeparam>
    public sealed class ServiceBusEmulatorTester<TFunction> : IAsyncDisposable where TFunction : class
    {
        private readonly string? _connectionString = null;
        private readonly string? _queueName = null;
        private readonly string? _topicName = null;
        private readonly string? _subscriptionName = null;
        private readonly ServiceBusClient? _client;
        private ServiceBusSender? _sender;
        private readonly TimeSpan _delay;
        private const int _waitSeconds = 3;

        /// <summary>
        /// Initializes a new <see cref="ServiceBusEmulatorTester{TFunction}"/> class.
        /// </summary>
        /// <param name="serviceScope">The <see cref="IServiceScope"/>.</param>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        /// <param name="methodName">An optional method name. Where not specified will attempt to automatically find a single method that has a parameter with <see cref="ServiceBusTriggerAttribute"/>.</param>
        /// <param name="delay">Adds the specified delay before any underlying Service Bus receive operation.</param>
        internal ServiceBusEmulatorTester(IServiceScope serviceScope, TestFrameworkImplementor implementor, string methodName, TimeSpan? delay)
        {
            ServiceScope = serviceScope ?? throw new ArgumentNullException(nameof(serviceScope));
            Implementor = implementor ?? throw new ArgumentNullException(nameof(implementor));
            _delay = delay ?? TimeSpan.Zero;

            var x = FindReceiveMethod(methodName);
            Method = x.Method!;
            Trigger = x.Trigger!;
            Parameter = x.Parameter;

            var config = ServiceScope.ServiceProvider.GetService<IConfiguration>();
            var (ConnectionString, QueueName, TopicName, SubscriptionName) = ServiceBusTriggerTester<TFunction>.VerifyServiceBusTriggerProperties(config, Trigger);
            _connectionString = ConnectionString;
            _queueName = QueueName;
            _topicName = TopicName;
            _subscriptionName = SubscriptionName;

            _client = new ServiceBusClient(_connectionString);
        }

        /// <summary>
        /// Gets the <see cref="IServiceScope"/>.
        /// </summary>
        internal IServiceScope ServiceScope { get; }

        /// <summary>
        /// Gets the <see cref="TestFrameworkImplementor"/>.
        /// </summary>
        internal TestFrameworkImplementor Implementor { get; }

        /// <summary>
        /// Gets the <see cref="MemberInfo"/>.
        /// </summary>
        internal MethodInfo Method { get; private set; }

        /// <summary>
        /// Gets the <see cref="ParameterInfo"/> for the <see cref="Method"/> parameter that uses the <see cref="Trigger"/>.
        /// </summary>
        internal ParameterInfo Parameter { get; private set; }

        /// <summary>
        /// Gets the <see cref="ServiceBusTriggerAttribute"/>.
        /// </summary>
        internal ServiceBusTriggerAttribute Trigger { get; private set; }

        /// <summary>
        /// Finds the method for the receive that meets the key requirements.
        /// </summary>
        private (MethodInfo Method, ParameterInfo Parameter, ServiceBusTriggerAttribute Trigger) FindReceiveMethod(string methodName)
        {
            if (string.IsNullOrEmpty(methodName))
                throw new ArgumentNullException(nameof(methodName));

            MethodInfo mi;
            try
            {
                mi = typeof(TFunction).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance) ?? throw new InvalidOperationException($"The trigger method '{methodName}' is either not a public instance method or does not exist.");
            }
            catch (AmbiguousMatchException ex)
            {
                throw new InvalidOperationException($"An unambiquous single trigger method '{methodName}' must only exists.", ex);
            }

            ParameterInfo? pi = null;
            ServiceBusTriggerAttribute? sbta = null;

            var type = typeof(TFunction);
            foreach (var m in type.GetMethods().Where(x => (methodName == null || (methodName != null && x.Name == methodName)) && x.IsPublic))
            {
                foreach (var p in m.GetParameters())
                {
                    var tr = p.GetCustomAttribute<ServiceBusTriggerAttribute>();
                    if (tr != null)
                    {
                        if (sbta != null)
                            throw new InvalidOperationException($"The trigger method '{methodName}' has more than one parameter that uses the '{nameof(ServiceBusTriggerAttribute)}'.");

                        pi = p;
                        sbta = tr;
                    }
                }
            }

            if (pi == null)
                throw new InvalidOperationException($"The trigger method '{methodName}' does not have a parameter using the '{nameof(ServiceBusTriggerAttribute)}'.");

            if (mi.ReturnType != typeof(Task))
                throw new InvalidOperationException($"The trigger method '{methodName}' must have a return Type of '{nameof(Task)}'.");

            return (mi, pi!, sbta!);
        }

        /// <summary>
        /// Creates a <see cref="ServiceBusReceiver"/>.
        /// </summary>
        private ServiceBusReceiver GetReceiver()
        {
            if (Trigger.IsSessionsEnabled)
                throw new NotSupportedException($"UnitTestEx does not support any Receive operations for Sessions; i.e. {nameof(ServiceBusTriggerAttribute)}.{nameof(ServiceBusTriggerAttribute.IsSessionsEnabled)} = true.");

            return _queueName == null ?
                _client!.CreateReceiver(_topicName!, _subscriptionName, new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.PeekLock }) :
                _client!.CreateReceiver(_queueName, new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.PeekLock });
        }

        /// <summary>
        /// Gets the <see cref="ServiceBusSender"/>.
        /// </summary>
        private ServiceBusSender GetSender() => _sender ??= _client!.CreateSender(_queueName ?? _topicName!);

        /// <summary>
        /// Clear (removes) all existing messages from the queue.
        /// </summary>
        public async Task ClearAsync()
        {
            await Task.Delay(_delay);
            Implementor.WriteLine("FUNCTION SERVICE BUS TRIGGER TESTER...");
            Implementor.WriteLine("CLEAR >");

            while (true)
            {
                await using var receiver = GetReceiver();
                var list = await receiver.ReceiveMessagesAsync(100, TimeSpan.FromSeconds(_waitSeconds)).ConfigureAwait(false);
                if (list.Count == 0)
                {
                    Implementor.WriteLine("No messages found.");
                    break;
                }

                foreach (var message in list)
                {
                    await receiver.CompleteMessageAsync(message).ConfigureAwait(false);
                }

                Implementor.WriteLine($"{list.Count} message(s) automatically completed.");
            }

            Implementor.WriteLine("");
            Implementor.WriteLine(new string('=', 80));
            Implementor.WriteLine("");
        }

        /// <summary>
        /// Peeks the next message.
        /// </summary>
        /// <param name="maxWaitTime">An optional <see cref="TimeSpan"/> specifying the maximum time to wait for a message before returning null if no messages are available.</param>
        /// <returns>The <see cref="ServiceBusReceivedMessage"/> where found; otherwise, <c>null</c>.</returns>
        public async Task<ServiceBusReceivedMessage?> PeekAsync(TimeSpan? maxWaitTime = null)
        {
            await Task.Delay(_delay);
            await using var receiver = GetReceiver();
            var msg = await receiver.ReceiveMessageAsync(maxWaitTime ?? TimeSpan.FromSeconds(_waitSeconds)).ConfigureAwait(false);
            if (msg != null)
                await receiver.AbandonMessageAsync(msg).ConfigureAwait(false);

            LogMessage(msg);
            return msg;
        }

        /// <summary>
        /// Sends a message where the <see cref="ServiceBusMessage.Body"/> <see cref="BinaryData"/> will contain the <paramref name="value"/> as serialized JSON.
        /// </summary>
        /// <typeparam name="T">The <paramref name="value"/> <see cref="Type"/>.</typeparam>
        /// <param name="value">The value.</param>
        /// <param name="messageModify">Optional <see cref="ServiceBusMessage"/> modifier than enables the message to be further configured.</param>
        public Task SendValueAsync<T>(T value, Action<ServiceBusMessage>? messageModify = null)
            => SendFromJsonAsync(JsonConvert.SerializeObject(value), messageModify);

        /// <summary>
        /// Sends a message where the <see cref="ServiceBusMessage.Body"/> <see cref="BinaryData"/> will contain the JSON formatted embedded resource as the content (<see cref="MediaTypeNames.Application.Json"/>).
        /// </summary>
        /// <typeparam name="TAssembly">The <see cref="Type"/> to infer <see cref="Type.Assembly"/> for the embedded resources.</typeparam>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name).</param>
        /// <param name="messageModify">Optional <see cref="ServiceBusMessage"/> modifier than enables the message to be further configured.</param>
        public Task SendFromResourceAsync<TAssembly>(string resourceName, Action<ServiceBusMessage>? messageModify = null)
            => SendFromResourceAsync(resourceName, messageModify, typeof(TAssembly).Assembly);

        /// <summary>
        /// Sends a message where the <see cref="ServiceBusMessage.Body"/> <see cref="BinaryData"/> will contain the JSON formatted embedded resource as the content (<see cref="MediaTypeNames.Application.Json"/>).
        /// </summary>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name).</param>
        /// <param name="messageModify">Optional <see cref="ServiceBusMessage"/> modifier than enables the message to be further configured.</param>
        /// <param name="assembly">The <see cref="Assembly"/> that contains the embedded resource; defaults to <see cref="Assembly.GetEntryAssembly()"/>.</param>
        public Task SendFromResourceAsync(string resourceName, Action<ServiceBusMessage>? messageModify = null, Assembly? assembly = null)
            => SendFromJsonAsync(Resource.GetString(resourceName, assembly ?? Assembly.GetCallingAssembly()), messageModify);

        /// <summary>
        /// Sends a message where the <see cref="ServiceBusMessage.Body"/> <see cref="BinaryData"/> will contain the serialized <paramref name="json"/>.
        /// </summary>
        /// <param name="json">The JSON body.</param>
        /// <param name="messageModify">Optional <see cref="ServiceBusMessage"/> modifier than enables the message to be further configured.</param>
        public async Task SendFromJsonAsync(string json, Action<ServiceBusMessage>? messageModify = null)
        {
            var message = new ServiceBusMessage(Encoding.UTF8.GetBytes(json ?? throw new ArgumentNullException(nameof(json))))
            {
                ContentType = "application/json",
                MessageId = Guid.NewGuid().ToString()
            };

            messageModify?.Invoke(message);
            await SendAsync(message).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends the <paramref name="messages"/>.
        /// </summary>
        /// <param name="messages">One or more <see cref="ServiceBusMessage"/> messages.</param>
        public async Task SendAsync(params ServiceBusMessage[] messages)
        {
            if (messages == null)
                return;

            Implementor.WriteLine("FUNCTION SERVICE BUS TRIGGER TESTER...");
            Implementor.WriteLine($"Send {messages.Length} message(s).");
            foreach (var message in messages)
            {
                LogMessage(message);
            }

            await GetSender().SendMessagesAsync(messages).ConfigureAwait(false);

            Implementor.WriteLine("");
            Implementor.WriteLine(new string('=', 80));
            Implementor.WriteLine("");
        }

        /// <summary>
        /// Runs the <i>Azure Function</i> run-time emulation by performing a <see cref="ServiceBusReceiver.ReceiveMessageAsync(TimeSpan?, System.Threading.CancellationToken)"/> as specified by the underlying <see cref="ServiceBusTriggerAttribute"/>.
        /// </summary>
        /// <param name="maxWaitTime">An optional <see cref="TimeSpan"/> specifying the maximum time to wait for a message before returning null if no messages are available.</param>
        /// <param name="autoCompleteOverride">Overrides the value indicating whether the trigger should automatically complete the message after successful processing. If not explicitly set, the behavior will be based on the <see cref="ServiceBusTriggerAttribute.AutoCompleteMessages"/>.</param>
        /// <returns>The <see cref="ServiceBusEmulatorRunAssertor"/>.</returns>
        /// <remarks>Only supports a <see cref="ServiceBusTriggerAttribute"/> where the corresponding value is a <see cref="ServiceBusReceivedMessage"/>. The only other Service Bus related parameter that is supported is a
        /// <see cref="ServiceBusMessageActions"/>. The <see cref="ServiceBusTriggerAttribute.Connection"/> and <see cref="ServiceBusTriggerAttribute.QueueName"/> (etc.) are loaded from <see cref="IConfiguration"/> using convention as expected.</remarks>
        public async Task<ServiceBusEmulatorRunAssertor> RunAsync(TimeSpan? maxWaitTime = null, bool? autoCompleteOverride = null)
        {
            await Task.Delay(_delay);
            var sbsrr = new ServiceBusEmulatorRunResult();

            // Create an instance of the function class.
            var instance = ServiceScope.ServiceProvider.CreateInstance<TFunction>();

            // Receive the next message from ServiceBus.
            var sw = Stopwatch.StartNew();
            await using var receiver = GetReceiver();
            sbsrr.Message = await receiver.ReceiveMessageAsync(maxWaitTime ?? TimeSpan.FromSeconds(_waitSeconds)).ConfigureAwait(false);
            if (sbsrr.Message == null)
            {
                sw.Stop();
                LogOutput(null, sw.ElapsedMilliseconds, sbsrr, autoCompleteOverride, "Queue/Topic empty; i.e. there are currently no messages.");
                return new ServiceBusEmulatorRunAssertor(sbsrr, null, Implementor);
            }

            var logger = ServiceScope.ServiceProvider.GetService<ILogger<ServiceBusTriggerTester<TFunction>>>();
            var sbma = new ServiceBusMessageActionsWrapper(receiver, logger);

            // Invoke the function method best as we can.
            var args = new List<object?>();
            foreach (var p in Method.GetParameters())
            {
                if (p == Parameter)
                    args.Add(GetMessageValue(sbsrr.Message));
                else if (p.ParameterType == typeof(ServiceBusMessageActions))
                    args.Add(sbma);
                else if (p.ParameterType == typeof(ILogger))
                    args.Add(logger);
                else
                    args.Add(ServiceBusEmulatorTester<TFunction>.GetNamedMessageValue(sbsrr.Message, p.Name!));
            }

            var autoComplete = autoCompleteOverride ?? Trigger.AutoCompleteMessages;

            // Execute the function.
            try
            {
                await ((Task)Method.Invoke(instance, args.ToArray())!).ConfigureAwait(false);

                // Where auto-completing then do so.
                if (autoComplete)
                    await sbma.CompleteMessageAsync(sbsrr.Message).ConfigureAwait(false);

                sw.Stop();
                sbsrr.SetUsingActionsWrapper(sbma);
                LogOutput(null, sw.ElapsedMilliseconds, sbsrr, autoCompleteOverride, null);
                return new ServiceBusEmulatorRunAssertor(sbsrr, null, Implementor);
            }
            catch (Exception ex)
            {
                sw.Stop();

                logger.LogWarning($"Exception bubbled out of the Function execution; this may be the desired the behavior: {ex.Message}");

                // Where unhandled then automatically abandon.
                await sbma.AbandonMessageAsync(sbsrr.Message).ConfigureAwait(false);

                sbsrr.SetUsingActionsWrapper(sbma);
                LogOutput(ex, sw.ElapsedMilliseconds, sbsrr, autoCompleteOverride, null);
                return new ServiceBusEmulatorRunAssertor(sbsrr, ex, Implementor);
            }
        }

        /// <summary>
        /// Gets the message value according to the <see cref="ParameterInfo.ParameterType"/>.
        /// </summary>
        private object? GetMessageValue(ServiceBusReceivedMessage msg)
        {
            if (Parameter.ParameterType == typeof(ServiceBusReceivedMessage))
                return msg;
            else if (Parameter.ParameterType == typeof(string))
                return msg.Body.ToString();
            else if (Parameter.ParameterType == typeof(byte[]))
                return msg.Body.ToArray();
            else if (Parameter.ParameterType == typeof(BinaryData))
                return msg.Body;
            else
            {
                var r = new System.Text.Json.Utf8JsonReader(msg.Body);
                return System.Text.Json.JsonSerializer.Deserialize(ref r, Parameter.ParameterType);
            }
        }

        /// <summary>
        /// Gets the parameter value from the message using the same name.
        /// </summary>
        private static object? GetNamedMessageValue(ServiceBusReceivedMessage msg, string name) => name.ToLower() switch
        {
            "messageid" => msg.MessageId,
            "sequencenumber" => msg.SequenceNumber,
            "subject" => msg.Subject,
            "label" => msg.Subject,
            "applicationproperties" => msg.ApplicationProperties,
            "contenttype" => msg.ContentType,
            "correlationid" => msg.CorrelationId,
            "deliverycount" => msg.DeliveryCount,
            "deadlettersource" => msg.DeadLetterSource,
            "enqueuedtime" => msg.EnqueuedTime,
            "expiresat" => msg.ExpiresAt,
            "enqueuedtimeutc" => msg.EnqueuedTime.UtcDateTime,
            "expiresatutc" => msg.ExpiresAt.UtcDateTime,
            "replyto" => msg.ReplyTo,
            "to" => msg.To,
            _ => throw new InvalidOperationException($"Method parameter named '{name}' is unknown and as such a value can not be assigned; this could be a limitation of this emulator not the actual Azure Functions runtime."),
        };

        /// <summary>
        /// Log the output.
        /// </summary>
        private void LogOutput(Exception? ex, long ms, ServiceBusEmulatorRunResult sbsrr, bool? autoCompleteOverride, string? extra)
        {
            Implementor.WriteLine("");
            Implementor.WriteLine("FUNCTION SERVICE BUS TRIGGER TESTER...");
            Implementor.WriteLine($"Method: {Method.DeclaringType?.Name}.{Method.Name}");
            Implementor.WriteLine($"AutoCompleteOverride: {autoCompleteOverride?.ToString() ?? "<none>"}");
            Implementor.WriteLine("");
            Implementor.WriteLine("TRIGGER >");
            Implementor.WriteLine($"Connection: {Trigger.Connection}");
            Implementor.WriteLine($"QueueName: {Trigger.QueueName} {(Trigger.QueueName == _queueName ? "" : $"= {_queueName}")}");
            Implementor.WriteLine($"TopicName: {Trigger.TopicName} {(Trigger.TopicName == _topicName ? "" : $"= {_topicName}")}");
            Implementor.WriteLine($"SubscriptionName: {Trigger.SubscriptionName} {(Trigger.SubscriptionName == _subscriptionName ? "" : $"= {_subscriptionName}")}");
            Implementor.WriteLine($"AutoCompleteMessages: {Trigger.AutoCompleteMessages}");
            Implementor.WriteLine($"IsSessionsEnabled: {Trigger.IsSessionsEnabled}");
            Implementor.WriteLine("");
            Implementor.WriteLine("RESULT >");
            Implementor.WriteLine($"Elapsed (ms): {ms}");
            Implementor.WriteLine($"MessageStatus: {sbsrr.Status}");
            if (sbsrr.DeadletterReason != null)
                Implementor.WriteLine($"DeadLetterReason: {sbsrr.DeadletterReason}");

            if (sbsrr.MessagePropertiesModified != null && sbsrr.MessagePropertiesModified.Count > 0)
            {
                Implementor.WriteLine($"MessagePropertiesModified:");
                foreach (var item in sbsrr.MessagePropertiesModified)
                {
                    Implementor.WriteLine($"  {item.Key}: {item.Value}");
                }
            }

            LogMessage(sbsrr.Message);

            if (ex != null)
            {
                Implementor.WriteLine("");
                Implementor.WriteLine($"Exception: {ex.Message} [{ex.GetType().Name}]");
                Implementor.WriteLine(ex.ToString());
            }

            if (extra != null)
            {
                Implementor.WriteLine("");
                Implementor.WriteLine(extra);
            }

            Implementor.WriteLine("");
            Implementor.WriteLine(new string('=', 80));
            Implementor.WriteLine("");
        }

        /// <summary>
        /// Log the <see cref="ServiceBusReceivedMessage"/>.
        /// </summary>
        private void LogMessage(ServiceBusReceivedMessage? msg)
        {
            if (msg == null)
            {
                Implementor.WriteLine("Message: <none>");
                return;
            }

            Implementor.WriteLine("");
            Implementor.WriteLine("MESSAGE >");
            Implementor.WriteLine($"MessageId: {msg.MessageId ?? "<null>"}");
            Implementor.WriteLine($"Subject: {msg.Subject ?? "<null>"}");
            Implementor.WriteLine($"SessionId: {msg.SessionId ?? "<null>"}");
            Implementor.WriteLine($"PartitionKey: {msg.PartitionKey ?? "<null>"}");
            Implementor.WriteLine($"CorrelationId: {msg.CorrelationId ?? "<null>"}");
            Implementor.WriteLine($"ContentType: {msg.ContentType ?? "<null>"}");

            if (msg.Body == null)
                Implementor.WriteLine($"Body: {msg.Body?.ToString() ?? "<null>"}");
            else
            {
                // Assume JSON and try to parse and output; where invalid simply output string representation.
                Implementor.WriteLine("Body:");
                try
                {
                    var jt = JToken.Parse(msg.Body.ToString());
                    Implementor.WriteLine(jt.ToString());
                }
                catch
                {
                    Implementor.WriteLine($"Body: {msg.Body}");
                }
            }
        }

        /// <summary>
        /// Log the <see cref="ServiceBusMessage"/>.
        /// </summary>
        private void LogMessage(ServiceBusMessage msg)
        {
            Implementor.WriteLine("");
            Implementor.WriteLine("MESSAGE >");
            Implementor.WriteLine($"MessageId: {msg.MessageId ?? "<null>"}");
            Implementor.WriteLine($"Subject: {msg.Subject ?? "<null>"}");
            Implementor.WriteLine($"SessionId: {msg.SessionId ?? "<null>"}");
            Implementor.WriteLine($"PartitionKey: {msg.PartitionKey ?? "<null>"}");
            Implementor.WriteLine($"CorrelationId: {msg.CorrelationId ?? "<null>"}");
            Implementor.WriteLine($"ContentType: {msg.ContentType ?? "<null>"}");

            if (msg.Body == null)
                Implementor.WriteLine($"Body: {msg.Body?.ToString() ?? "<null>"}");
            else
            {
                // Assume JSON and try to parse and output; where invalid simply output string representation.
                Implementor.WriteLine("Body:");
                try
                {
                    var jt = JToken.Parse(msg.Body.ToString());
                    Implementor.WriteLine(jt.ToString());
                }
                catch
                {
                    Implementor.WriteLine($"Body: {msg.Body}");
                }
            }
        }

        /// <summary>
        /// Disposes of all resources.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_sender != null)
                await _sender.DisposeAsync();

            if (_client != null)
                await _client.DisposeAsync().ConfigureAwait(false);
        }
    }
}