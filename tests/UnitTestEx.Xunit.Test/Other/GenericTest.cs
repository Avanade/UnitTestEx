using UnitTestEx.Expectations;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestEx.Xunit.Test.Other
{
    public class GenericTest : UnitTestBase
    {
        public GenericTest(ITestOutputHelper output) : base(output) { }

        [Fact]
        public void Run_Success()
        {
            using var test = CreateGenericTester();
            test.Run(() => 1)
                .AssertSuccess()
                .AssertValue(1);
        }

        [Fact]
        public void Run_Exception()
        {
            using var test = CreateGenericTester();
            test.ExpectError("Badness.")
                .Run(() => throw new System.ArithmeticException("Badness."));
        }
    }
}