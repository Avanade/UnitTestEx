using NUnit.Framework;
using NUnit.Framework.Internal;
using System;
using System.Text.Json;
using UnitTestEx.Assertors;
using UnitTestEx.Json;

namespace UnitTestEx.NUnit.Test.Other
{
    [TestFixture]
    public class ValueAssertorTest
    {
        [Test]
        public void AssertJson_UsesStructuralComparison()
        {
            using var test = GenericTester.Create();
            var value = new { a = 1, b = new[] { new { text = "hello", number = 2 } } };
            new ValueAssertor<object>(test, value, null, null)
                .AssertJson(""" { "b": [ { "number": 2.0, "text": "\u0068ello" } ], "a": 1.0 } """);
        }

        [Test]
        public void AssertJson_IgnoresObjectPaths()
        {
            using var test = GenericTester.Create();
            new ValueAssertor<object>(test, new { a = new { b = 1 }, c = 2 }, null, null)
                .AssertJson("""{"c":2,"a":{"b":99}}""", "a.b");
        }

        [Test]
        public void AssertJson_ReportsStructuralDifference()
        {
            using var test = GenericTester.Create();
            var assertor = new ValueAssertor<object>(test, new { a = new { b = 1 } }, null, null);
            var exception = Failure(() => assertor.AssertJson("""{"a":{"b":2}}"""));
            Assert.That(exception.Message, Does.Contain("a.b"));
        }

        [Test]
        public void AssertJson_ArrayOrderRemainsSignificant()
        {
            using var test = GenericTester.Create();
            var assertor = new ValueAssertor<int[]>(test, [1, 2], null, null);
            Failure(() => assertor.AssertJson("[2,1]"));
        }

        [TestCase("null", null)]
        [TestCase("true", true)]
        [TestCase("1.0", 1)]
        [TestCase("\"hello\"", "hello")]
        [TestCase("\"{\\\"a\\\":1}\"", "{\"a\":1}")]
        public void AssertJson_ScalarValues(string json, object value)
        {
            using var test = GenericTester.Create();
            new ValueAssertor<object>(test, value, null, null).AssertJson(json);
        }

        [Test]
        public void AssertJson_InvalidJsonIsRejected()
        {
            using var test = GenericTester.Create();
            var assertor = new ValueAssertor<int>(test, 1, null, null);
            Assert.Catch<JsonException>(() => assertor.AssertJson("{ not json"));
        }

        [Test]
        public void AssertJson_UsesOwnerSerializerAndOptions()
        {
            using var test = GenericTester.Create()
                .UseJsonSerializer(new UnitTestEx.Json.JsonSerializer(new JsonSerializerOptions { PropertyNamingPolicy = null }))
                .UseJsonComparerOptions(new JsonElementComparerOptions { PropertyNameComparer = StringComparer.OrdinalIgnoreCase });
            var assertor = new ValueAssertor<object>(test, new { FirstName = "Jane" }, null, null);
            assertor.AssertJson("""{"FIRSTNAME":"Jane"}""");
            test.UseJsonComparerOptions(new JsonElementComparerOptions());
            Failure(() => assertor.AssertJson("""{"firstName":"Jane"}"""));
            assertor.AssertJson("""{"FirstName":"Jane"}""");
        }

        [Test]
        public void AssertJson_UsesSemanticAndExactValueOptions()
        {
            using var test = GenericTester.Create();
            var assertor = new ValueAssertor<object>(test, new { date = "2024-01-01T00:00:00Z" }, null, null);
            const string json = """{"date":"2024-01-01T00:00:00.000+00:00"}""";
            assertor.AssertJson(json);
            test.UseJsonComparerOptions(new JsonElementComparerOptions { ValueComparison = JsonElementComparison.Exact });
            Failure(() => assertor.AssertJson(json));
        }

        [Test]
        public void JsonElementComparer_HashCodeMatchesSemanticEquality()
        {
            var comparer = new JsonElementComparer(new JsonElementComparerOptions
            {
                PropertyNameComparer = StringComparer.OrdinalIgnoreCase,
                NullComparison = JsonElementComparison.Semantic
            });

            const string first = """{"Name":"2024-01-01T00:00:00Z","Count":1.0,"Ignored":null}""";
            const string second = """{"count":1,"name":"2024-01-01T00:00:00.000+00:00"}""";

            Assert.That(comparer.Equals(first, second), Is.True);
            Assert.That(comparer.GetHashCode(first), Is.EqualTo(comparer.GetHashCode(second)));
            Assert.DoesNotThrow(() => comparer.GetHashCode("""{"value":1e1000}"""));
        }

        [Test]
        public void AssertJsonFromResource_BothOverloadsCompareStructurally()
        {
            using var test = GenericTester.Create()
                .UseJsonSerializer(new UnitTestEx.Json.JsonSerializer(new JsonSerializerOptions()));
            new ValueAssertor<object>(test, new { LastName = "Smith", FirstName = "Rachel" }, null, null)
                .AssertJsonFromResource("FunctionTest-ValidJsonResource.json")
                .AssertJsonFromResource<ValueAssertorTest>("FunctionTest-ValidJsonResource.json");
        }

        private static AssertionException Failure(TestDelegate assertion)
        {
            using var context = new TestExecutionContext.IsolatedContext();
            return Assert.Throws<AssertionException>(assertion)!;
        }
    }
}
