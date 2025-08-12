using System;
using System.Threading.Tasks;
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
            using var test = GenericTester.Create();
            test.Run(() => 1)
                .AssertSuccess()
                .AssertValue(1);
        }

        [Fact]
        public void Run_Exception()
        {
            static Func<Task> ThrowBadness() => () => throw new DivideByZeroException("Badness.");

            using var test = GenericTester.Create();
            test.ExpectError("Badness.")
                .Run(ThrowBadness());
        }
    }
}