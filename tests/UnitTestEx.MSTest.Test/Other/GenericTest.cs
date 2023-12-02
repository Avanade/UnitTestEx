using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using UnitTestEx.Expectations;

namespace UnitTestEx.MSTest.Test.Other
{
    [TestClass]
    public class GenericTest
    {
        [TestMethod]
        public void Run_Success()
        {
            using var test = GenericTester.Create();
            test.Run(() => 1)
                .AssertSuccess()
                .AssertValue(1);
        }

        [TestMethod]
        public void Run_Exception()
        {
            using var test = GenericTester.Create();
            test.ExpectError("Badness.")
                .Run(() => throw new DivideByZeroException("Badness."));
        }
    }
}