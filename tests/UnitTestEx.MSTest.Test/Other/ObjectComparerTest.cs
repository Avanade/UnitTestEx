using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using UnitTestEx.MSTest.Test.Model;

namespace UnitTestEx.MSTest.Test.Other
{
    [TestClass]
    [Obsolete($"This is being replaced by the {nameof(UnitTestEx.Abstractions.JsonElementComparer)} and usage of paths to ignore (versus members) to be more explicit.")]
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