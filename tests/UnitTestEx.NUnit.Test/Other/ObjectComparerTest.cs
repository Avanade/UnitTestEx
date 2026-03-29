using NUnit.Framework;
using UnitTestEx.NUnit.Test.Model;

namespace UnitTestEx.NUnit.Test.Other
{
    [TestFixture]
    public class ObjectComparerTest
    {
        [Test]
        public void MatchyMatchy()
        {
            var p1 = new Person { FirstName = "Wendy", LastName = "Brown" };
            var p2 = new Person { FirstName = "Wendy", LastName = "Brown" };
            ObjectComparer.Assert(p1, p2);
        }

        [Test]
        public void PathsToIgnore_PropertyName()
        {
            var p1 = new Person { FirstName = "Wendy", LastName = "XXX" };
            var p2 = new Person { FirstName = "Wendy", LastName = "YYY" };
            ObjectComparer.Assert(p1, p2, "LastName");
        }

        [Test]
        public void PathsToIgnore_Json()
        {
            // Starting array and object are ignored, so the path to ignore is just the property name.
            var j1 = @"[ { ""FirstName"": ""Wendy"", ""LastName"": ""XXX"" } ]";
            var j2 = @"[ { ""FirstName"": ""Wendy"", ""LastName"": ""YYY"" } ]";
            ObjectComparer.AssertJson(j1, j2, "LastName");
        }
    }
}