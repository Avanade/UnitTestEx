using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;
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
            static Func<Task> ThrowBadness() => () => throw new DivideByZeroException("Badness.");

            using var test = GenericTester.Create();
            test.ExpectError("Badness.")
                .Run(ThrowBadness());
        }

        [TestMethod]
        public void Run_Exception_ValueTask()
        {
            static Func<ValueTask> ThrowBadness() => () => throw new DivideByZeroException("Badness.");

            using var test = GenericTester.Create();
            test.ExpectError("Badness.")
                .Run(ThrowBadness());
        }
    }
}