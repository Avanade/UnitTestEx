using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTestEx.MSTest.Test.Model;

namespace UnitTestEx.MSTest.Test.Other
{
    [TestClass]
    public class ObjectComparerTest
    {
        [TestMethod]
        public void Test()
        {
            var p1 = new Person { FirstName = "Wendy", LastName = "Brown" };
            var p2 = new Person { FirstName = "Wendy", LastName = "Brown" };
            ObjectComparer.Assert(p1, p2);
        }
    }
}