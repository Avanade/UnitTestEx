using NUnit.Framework;
using UnitTestEx.NUnit.Test.Model;

namespace UnitTestEx.NUnit.Test.Other
{
    [TestFixture]
    public class ObjectComparerTest
    {
        [Test]
        public void Test()
        {
            var p1 = new Person { FirstName = "Wendy", LastName = "Brown" };
            var p2 = new Person { FirstName = "Wendy", LastName = "Brown" };
            ObjectComparer.Assert(p1, p2);
        }
    }
}