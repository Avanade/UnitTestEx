using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
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
                .AssertValue(1);
        }

        [Test]
        public void Run_Exception()
        {
            using var test = GenericTester.Create();
            test.ExpectException("Badness.")
                .Run(() => throw new DivideByZeroException("Badness."));
        }

        [Test]
        public void Run_Service()
        {
            using var test = GenericTester.Create().ConfigureServices(services => services.AddSingleton<Gin>());

            test.Run<Gin, int>(gin => gin.Pour())
                .AssertSuccess()
                .AssertValue(1);

            test.Run<Gin, int>(gin => gin.PourAsync())
                .AssertSuccess()
                .AssertValue(1);

            test.Run<Gin>(gin => gin.Shake())
                .AssertSuccess();

            test.Run<Gin>(gin => gin.ShakeAsync())
                .AssertSuccess();

            test.Run<Gin>(gin => gin.Stir())
                .AssertException<DivideByZeroException>("As required by Bond; shaken, not stirred.");

            test.Run<Gin>(gin => gin.StirAsync())
                .AssertException<DivideByZeroException>("As required by Bond; shaken, not stirred.");
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Just for testing aye bro!")]
    public class Gin
    {
        public void Stir() => throw new DivideByZeroException("As required by Bond; shaken, not stirred.");
        public Task StirAsync() => throw new DivideByZeroException("As required by Bond; shaken, not stirred.");
        public void Shake() { }
        public Task ShakeAsync() => Task.CompletedTask;
        public int Pour() => 1;
        public Task<int> PourAsync() => Task.FromResult(1);
    }
}