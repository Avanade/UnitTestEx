using UnitTestEx.Xunit.Test.Model;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestEx.Xunit.Test.Other
{
    public class ObjectComparerTest : UnitTestBase
    {
        public ObjectComparerTest(ITestOutputHelper output) : base(output) { }

        [Fact]
        public void Test()
        {
            var p1 = new Person { FirstName = "Wendy", LastName = "Brown" };
            var p2 = new Person { FirstName = "Wendy", LastName = "Brown" };
            ObjectComparer.Assert(p1, p2);
        }
    }
}