using Azure.Messaging;
using Azure.Messaging.EventGrid;
using System;
using System.Text.Json;
using NUnit.Framework;
using UnitTestEx.Azure.Functions;
using UnitTestEx.IsolatedFunction;

namespace UnitTestEx.NUnit.Test
{
    [TestFixture]
    public class EventGridExtensionMethodsTest
    {
        [Test]
        public void BlobCreatedEvent_ContainsBlobSubjectAndProperties()
        {
            var blobUri = new Uri("http://127.0.0.1:10000/devstoreaccount1/imports/batch/customers.csv");

            var evt = EventGridExtensionMethods.CreateBlobCreatedEvent("imports", "batch/customers.csv", blobUri, "\"etag-value\"", "text/csv", 123);
            using var data = JsonDocument.Parse(evt.Data.ToString());

            Assert.Multiple(() =>
            {
                Assert.That(evt.Subject, Is.EqualTo("/blobServices/default/containers/imports/blobs/batch/customers.csv"));
                Assert.That(evt.EventType, Is.EqualTo("Microsoft.Storage.BlobCreated"));
                Assert.That(evt.DataVersion, Is.EqualTo("1.0"));
                Assert.That(data.RootElement.GetProperty("url").GetString(), Is.EqualTo(blobUri.AbsoluteUri));
                Assert.That(data.RootElement.GetProperty("eTag").GetString(), Is.EqualTo("\"etag-value\""));
                Assert.That(data.RootElement.GetProperty("contentType").GetString(), Is.EqualTo("text/csv"));
                Assert.That(data.RootElement.GetProperty("contentLength").GetInt64(), Is.EqualTo(123));
                Assert.That(data.RootElement.GetProperty("blobType").GetString(), Is.EqualTo("BlockBlob"));
            });
        }

        [Test]
        public void EventGridEvent_FromValue()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            var value = new Person("Jane", "Doe");

            var evt = test.CreateEventGridEventFromValue(value, "/people/1", "People.Created");

            Assert.Multiple(() =>
            {
                Assert.That(evt.Subject, Is.EqualTo("/people/1"));
                Assert.That(evt.EventType, Is.EqualTo("People.Created"));
                Assert.That(evt.DataVersion, Is.EqualTo("1.0"));
                Assert.That(evt.Data.ToString(), Is.EqualTo(test.JsonSerializer.Serialize(value)));
            });
        }

        [Test]
        public void EventGridEvent_FromJson()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            const string json = """{"name":"Jane"}""";

            var evt = test.CreateEventGridEventFromJson(json, "/people/1", "People.Created");

            Assert.That(evt.Data.ToString(), Is.EqualTo(json));
        }

        [Test]
        public void EventGridEvent_FromResource()
        {
            using var test = FunctionTester.Create<HttpFunction>();

            var evt = test.CreateEventGridEventFromResource<IsolatedFunctionTest>("FunctionTest-ValidJsonResource.json", "/people/1", "People.Created");

            Assert.That(evt.Data.ToString(), Is.EqualTo(Resource.GetJson("FunctionTest-ValidJsonResource.json", typeof(EventGridExtensionMethodsTest).Assembly)));
        }

        [Test]
        public void CloudEvent_FromValue()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            var value = new Person("Jane", "Doe");

            var evt = test.CreateCloudEventFromValue(value, "/people", "People.Created");

            Assert.Multiple(() =>
            {
                Assert.That(evt.Source, Is.EqualTo("/people"));
                Assert.That(evt.Type, Is.EqualTo("People.Created"));
                Assert.That(evt.DataContentType, Is.EqualTo("application/json"));
                Assert.That(evt.Data?.ToString(), Is.EqualTo(test.JsonSerializer.Serialize(value)));
            });
        }

        [Test]
        public void CloudEvent_FromJson()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            const string json = """{"name":"Jane"}""";

            var evt = test.CreateCloudEventFromJson(json, "/people", "People.Created");

            Assert.That(evt.Data?.ToString(), Is.EqualTo(json));
        }

        [Test]
        public void CloudEvent_FromResource()
        {
            using var test = FunctionTester.Create<HttpFunction>();

            var evt = test.CreateCloudEventFromResource<IsolatedFunctionTest>("FunctionTest-ValidJsonResource.json", "/people", "People.Created");

            Assert.That(evt.Data?.ToString(), Is.EqualTo(Resource.GetJson("FunctionTest-ValidJsonResource.json", typeof(EventGridExtensionMethodsTest).Assembly)));
        }
    }
}
