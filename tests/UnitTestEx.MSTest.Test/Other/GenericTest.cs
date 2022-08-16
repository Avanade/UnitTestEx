using Microsoft.VisualStudio.TestTools.UnitTesting;
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
                .Assert(1);
        }

        [TestMethod]
        public void Run_Exception()
        {
            using var test = GenericTester.Create();
            test.ExpectErrorType(CoreEx.Abstractions.ErrorType.ValidationError, "Badness.")
                .Run(() => throw new CoreEx.ValidationException("Badness."));
        }
    }
}