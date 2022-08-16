using NUnit.Framework;
using UnitTestEx.Expectations;

namespace UnitTestEx.NUnit.Test.Other
{
    [TestFixture]
    public class GenericTest
    {
        [Test]
        public void Run_Success()
        {
            using var test = GenericTester.Create();
            test.Run(() => 1)
                .AssertSuccess()
                .Assert(1);
        }

        [Test]
        public void Run_Exception()
        {
            using var test = GenericTester.Create();
            test.ExpectErrorType(CoreEx.Abstractions.ErrorType.ValidationError, "Badness.")
                .Run(() => throw new CoreEx.ValidationException("Badness."));
        }
    }
}