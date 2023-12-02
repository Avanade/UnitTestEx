// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Azure.Messaging.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.Assertors;
using UnitTestEx.Expectations;
using UnitTestEx.Hosting;
using UnitTestEx.Json;

namespace UnitTestEx.Functions
{
    /// <summary>
    /// Provides Azure Function <see cref="ServiceBusTriggerAttribute"/> unit-testing and integration emulation testing capabilities.
    /// </summary>
    /// <typeparam name="TFunction">The Azure Function <see cref="Type"/>.</typeparam>
    public class ServiceBusTriggerTester<TFunction> : HostTesterBase<TFunction>, IExpectations<ServiceBusTriggerTester<TFunction>> where TFunction : class
    {
        private static readonly Semaphore _semaphore = new(1, 1);

        /// <summary>
        /// Initializes a new <see cref="ServiceBusTriggerTester{TFunction}"/> class.
        /// </summary>
        /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
        /// <param name="serviceScope">The <see cref="IServiceScope"/>.</param>
        public ServiceBusTriggerTester(TesterBase owner, IServiceScope serviceScope) : base(owner, serviceScope) => ExpectationsArranger = new ExpectationsArranger<ServiceBusTriggerTester<TFunction>>(owner, this);

        /// <summary>
        /// Gets the <see cref="ExpectationsArranger{TSelf}"/>.
        /// </summary>
        public ExpectationsArranger<ServiceBusTriggerTester<TFunction>> ExpectationsArranger { get; }

        /// <summary>
        /// Runs the Service Bus Triggered (see <see cref="ServiceBusTriggerAttribute"/>) function expected as a parameter within the <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">The function operation invocation expression.</param>
        /// <param name="validateTriggerProperties">Indicates whether to validate the <see cref="ServiceBusTriggerAttribute"/> properties to ensure correct configuration.</param>
        /// <returns>A <see cref="VoidAssertor"/>.</returns>
        public VoidAssertor Run(Expression<Func<TFunction, Task>> expression, bool validateTriggerProperties = false) => RunAsync(expression, validateTriggerProperties).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the Service Bus Triggered (see <see cref="ServiceBusTriggerAttribute"/>) function expected as a parameter within the <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">The function operation invocation expression.</param>
        /// <param name="validateTriggerProperties">Indicates whether to validate the <see cref="ServiceBusTriggerAttribute"/> properties to ensure correct configuration.</param>
        /// <returns>A <see cref="VoidAssertor"/>.</returns>
        public async Task<VoidAssertor> RunAsync(Expression<Func<TFunction, Task>> expression, bool validateTriggerProperties = false)
        {
            object? sbv = null;
            ServiceBusMessageActionsAssertor? sba = null;
            ServiceBusSessionMessageActionsAssertor? ssba = null;
            (Exception? ex, double ms) = await RunAsync(expression, typeof(ServiceBusTriggerAttribute), (p, a, v) =>
            {
                if (a == null)
                    throw new InvalidOperationException($"The function method must have a parameter using the {nameof(ServiceBusTriggerAttribute)}.");

                Implementor.WriteLine("");
                Implementor.WriteLine("FUNCTION SERVICE BUS TRIGGER TESTER...");

                sbv = v;
                foreach (var pi in p)
                {
                    if (pi is ServiceBusReceivedMessage sbrm)
                        sbv = sbrm;
                    else if (pi is ServiceBusMessageActionsAssertor psba)
                        sba = psba;
                    else if (pi is ServiceBusSessionMessageActionsAssertor pssba)
                        ssba = pssba;
                }

                if (validateTriggerProperties)
                {
                    var config = ServiceScope.ServiceProvider.GetRequiredService<IConfiguration>();
                    VerifyServiceBusTriggerProperties(config, (ServiceBusTriggerAttribute)a);
                }
            }).ConfigureAwait(false);

            await Task.Delay(TestSetUp.TaskDelayMilliseconds).ConfigureAwait(false);
            var logs = Owner.SharedState.GetLoggerMessages();
            LogOutput(ex, ms, sbv, sba, ssba, logs);

            await ExpectationsArranger.AssertAsync(logs, ex).ConfigureAwait(false);

            return new VoidAssertor(Owner, ex);
        }

        /// <summary>
        /// Verifies the service bus trigger properties.
        /// </summary>
        internal static (string ConnectionString, string? QueueName, string? TopicName, string? SubscriptionName) VerifyServiceBusTriggerProperties(IConfiguration config, ServiceBusTriggerAttribute sbta)
        {
            // Get the connection string.
            var csn = sbta.Connection ?? "ServiceBus";
            var cs = config.GetValue<string?>(csn);
            if (cs == null)
            {
                csn = $"AzureWebJobs{csn}";
                cs = config.GetValue<string?>(csn);
            }

            if (string.IsNullOrEmpty(cs))
                throw new InvalidOperationException("Service Bus Connection String configuration setting either does not exist or does not have a value.");

            // Get the queue name.
            string? qn = null;
            string? tn = null;
            string? sn = null;
            if (sbta.QueueName != null)
                qn = GetValueFromConfig(config, "Queue Name", sbta.QueueName);
            else
            {
                tn = GetValueFromConfig(config, "Topic Name", sbta.TopicName);
                sn = GetValueFromConfig(config, "Subscription Name", sbta.SubscriptionName);
            }

            return (cs, qn, tn, sn);
        }

        /// <summary>
        /// Gets the value from configuration.
        /// </summary>
        internal static string GetValueFromConfig(IConfiguration config, string type, string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new InvalidOperationException($"Service Bus {type} does not have a valid value specified; is either null or empty.");

            if (name.StartsWith('%') && name.EndsWith('%'))
            {
                if (name.Length <= 2)
                    throw new InvalidOperationException($"Service Bus {type} name '{name}' uses a '%' prefix and suffix; the length must be greater greater than two to contain a configuration name within.");

                var cv = config.GetValue<string>(name[1..^1]);
                if (string.IsNullOrEmpty(cv))
                    throw new InvalidOperationException($"Service Bus {type} name '{name}' configuration setting either does not exist or does not have a value.");

                return cv;
            }

            return name;
        }

        /// <summary>
        /// Log the output.
        /// </summary>
        private void LogOutput(Exception? ex, double ms, object? value, ServiceBusMessageActionsAssertor? sba, ServiceBusSessionMessageActionsAssertor? ssba, IEnumerable<string?>? logs)
        {
            Implementor.WriteLine("");
            Implementor.WriteLine("LOGGING >");
            if (logs is not null && logs.Any())
            {
                foreach (var msg in logs)
                {
                    Implementor.WriteLine(msg);
                }
            }
            else
                Implementor.WriteLine("None.");

            Implementor.WriteLine("");
            Implementor.WriteLine("RESULT >");
            Implementor.WriteLine($"Elapsed (ms): {ms}");
            Implementor.WriteLine($"Message Type: {(value == null ? "<null>" : value.GetType().Name)}");

            if (value == null)
                Implementor.WriteLine("Message Value: <null>");
            else if (value is string str)
                Implementor.WriteLine($"Message Value: {str}");
            else if (value is IFormattable ifm)
                Implementor.WriteLine($"Message Value: {ifm}");
            else if (value is ServiceBusReceivedMessage sbrm)
            {
                Implementor.WriteLine("");
                Implementor.WriteLine("MESSAGE >");
                Implementor.WriteLine($"MessageId: {sbrm.MessageId ?? "<null>"}");
                Implementor.WriteLine($"Subject: {sbrm.Subject ?? "<null>"}");
                Implementor.WriteLine($"SessionId: {sbrm.SessionId ?? "<null>"}");
                Implementor.WriteLine($"PartitionKey: {sbrm.PartitionKey ?? "<null>"}");
                Implementor.WriteLine($"CorrelationId: {sbrm.CorrelationId ?? "<null>"}");
                Implementor.WriteLine($"ContentType: {sbrm.ContentType ?? "<null>"}");

                if (sbrm.ContentType == MediaTypeNames.Application.Json && sbrm.Body != null)
                {
                    Implementor.WriteLine("Body:");
                    try
                    {
                        var jt = JsonSerializer.Deserialize(sbrm.Body.ToString());
                        var json = JsonSerializer.Serialize(jt, JsonWriteFormat.Indented);
                        Implementor.WriteLine(json);
                    }
                    catch
                    {
                        Implementor.WriteLine($"Body: {sbrm.Body}");
                    }
                }
                else
                    Implementor.WriteLine($"Body: {sbrm.Body?.ToString() ?? "<null>"}");
            }
            else
            {
                Implementor.WriteLine("Message Value:");
                Implementor.WriteLine(JsonSerializer.Serialize(value, JsonWriteFormat.Indented));
            }

            if (ex != null)
            {
                Implementor.WriteLine("");
                Implementor.WriteLine($"Exception: {ex.Message} [{ex.GetType().Name}]");
                Implementor.WriteLine(ex.ToString());
            }

            sba?.LogResult();
            ssba?.LogResult();

            Implementor.WriteLine("");
            Implementor.WriteLine(new string('=', 80));
            Implementor.WriteLine("");
        }
    }
}