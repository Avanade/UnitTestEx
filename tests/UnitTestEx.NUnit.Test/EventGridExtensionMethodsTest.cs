using Azure.Messaging;
using Azure.Messaging.EventGrid;
using NUnit.Framework;
using UnitTestEx.Azure.Functions;
using UnitTestEx.IsolatedFunction;

namespace UnitTestEx.NUnit.Test
{
    [TestFixture]
    public class EventGridExtensionMethodsTest
    {
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
