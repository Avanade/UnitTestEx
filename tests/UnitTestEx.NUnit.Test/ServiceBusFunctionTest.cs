﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using UnitTestEx.Function;
using UnitTestEx.Json;

namespace UnitTestEx.NUnit.Test
{
    [TestFixture]
    public class ServiceBusFunctionTest
    {
        [Test]
        public void Object_Success()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://somesys"))
                .Request(HttpMethod.Post, "person").WithJsonBody(new { firstName = "Bob", lastName = "Smith" }).Respond.With(HttpStatusCode.OK);

            using var test = FunctionTester.Create<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .ServiceBusTrigger<ServiceBusFunction>()
                .Run(f => f.Run(new Person { FirstName = "Bob", LastName = "Smith" }, test.Logger))
                .AssertSuccess();

            mcf.VerifyAll();
        }

        [Test]
        public void Object_HttpClientError()
        {
            var mcf = MockHttpClientFactory.Create().UseJsonComparerOptions(new JsonElementComparerOptions { NullComparison = JsonElementComparison.Semantic });
            mcf.CreateClient("XXX", new Uri("https://somesys"))
                .Request(HttpMethod.Post, "person").WithJsonBody(new { firstName = "Bob", lastName = (string)null }).Respond.With(HttpStatusCode.InternalServerError);

            using var test = FunctionTester.Create<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .Type<ServiceBusFunction>()
                .Run(f => f.Run(new Person { FirstName = "Bob", LastName = (string)null }, test.Logger))
                .AssertException<HttpRequestException>("Response status code does not indicate success: 500 (Internal Server Error).");

            mcf.VerifyAll();
        }

        [Test]
        public void Object_ThrowsException()
        {
            var mcf = MockHttpClientFactory.Create();

            using var test = FunctionTester.Create<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .ServiceBusTrigger<ServiceBusFunction>()
                .Run(f => f.Run(new Person { FirstName = null, LastName = "Smith" }, test.Logger))
                .AssertException<InvalidOperationException>("First name is required.");

            mcf.VerifyAll();
        }

        [Test]
        public void ServiceBusMessage_Success()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://somesys"))
                .Request(HttpMethod.Post, "person").WithJsonBody(new { firstName = "Bob", lastName = "Smith" }).Respond.With(HttpStatusCode.OK);

            using var test = FunctionTester.Create<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .ServiceBusTrigger<ServiceBusFunction>()
                .Run(f => f.Run2(test.CreateServiceBusMessageFromValue(new Person { FirstName = "Bob", LastName = "Smith" }), test.Logger))
                .AssertSuccess();

            mcf.VerifyAll();
        }

        [Test]
        public void ServiceBusMessage_HttpClientError()
        {
            var mcf = MockHttpClientFactory.Create().UseJsonSerializer(new Json.JsonSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            mcf.CreateClient("XXX", new Uri("https://somesys"))
                .Request(HttpMethod.Post, "person").WithJsonBody(new { firstName = "Bob", lastName = (string)null }).Respond.With(HttpStatusCode.InternalServerError);

            using var test = FunctionTester.Create<Startup>();
            var r = test.ReplaceHttpClientFactory(mcf)
                .ServiceBusTrigger<ServiceBusFunction>()
                .Run(f => f.Run2(test.CreateServiceBusMessageFromValue(new Person { FirstName = "Bob", LastName = (string)null }), test.Logger))
                .AssertException<HttpRequestException>("Response status code does not indicate success: 500 (Internal Server Error).");

            mcf.VerifyAll();
        }

        [Test]
        public void ServiceBusMessage_ThrowsException()
        {
            var mcf = MockHttpClientFactory.Create();

            using var test = FunctionTester.Create<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .ServiceBusTrigger<ServiceBusFunction>()
                .Run(f => f.Run2(test.CreateServiceBusMessageFromValue(new Person { FirstName = null, LastName = "Smith" }), test.Logger))
                .AssertException<InvalidOperationException>("First name is required.");

            mcf.VerifyAll();
        }

        [Test]
        public void ServiceBusMessage_AssertActions()
        {
            using var test = FunctionTester.Create<Startup>();
            var msg = test.CreateServiceBusMessageFromValue(new Person { FirstName = null, LastName = "Smith" });
            var act = test.CreateWebJobsServiceBusMessageActions();

            test.ServiceBusTrigger<ServiceBusFunction>()
                .Run(f => f.Run3(msg, act, test.Logger))
                .AssertSuccess();

            act.AssertDeadLetter("Validation error", "First name is required");
        }

        [Test]
        public void ServiceProvider()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://somesys")).Request(HttpMethod.Get, "test").Respond.With("test output");

            using var test = FunctionTester.Create<Startup>();
            var hc = test.ReplaceHttpClientFactory(mcf)
                .Services.GetService<IHttpClientFactory>().CreateClient("XXX");

            var r = hc.GetAsync("test").Result;
            Assert.That(r, Is.Not.Null);
            Assert.That(r.Content.ReadAsStringAsync().Result, Is.EqualTo("test output"));
        }

        [Test]
        public void Configuration()
        {
            using var test = FunctionTester.Create<Startup>();
            var cv = test.Configuration.GetValue<string>("SpecialKey");
            Assert.That(cv, Is.EqualTo("VerySpecialValue"));
        }

        [Test]
        public void Configuration_Overrride()
        {
            // Demonstrates how to override the configuration settings for a test.
            using var test = FunctionTester.Create<Startup>(additionalConfiguration: [new("SpecialKey", "NotSoSpecial")]);
            var cv = test.Configuration.GetValue<string>("SpecialKey");
            Assert.That(cv, Is.EqualTo("NotSoSpecial"));
        }

        [Test]
        public void Configuration_Overrride_Use()
        {
            // Demonstrates how to override the configuration settings for a test.
            using var test = FunctionTester.Create<Startup>();
            test.UseAdditionalConfiguration([new("SpecialKey", "NotSoSpecial")]);
            var cv = test.Configuration.GetValue<string>("SpecialKey");
            Assert.That(cv, Is.EqualTo("NotSoSpecial"));
        }
    }
}