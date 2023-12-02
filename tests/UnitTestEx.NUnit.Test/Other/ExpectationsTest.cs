using NUnit.Framework;
using NUnit.Framework.Internal;
using System;
using System.Threading.Tasks;
using UnitTestEx.Expectations;

namespace UnitTestEx.NUnit.Test.Other
{
    [TestFixture]
    public class ExpectationsTest
    {
        [Test]
        public void ExceptionSuccess_ExpectException_Any()
        {
            var gt = GenericTester.Create().ExpectException().Any();

            var ex = Assert.Throws<Exception>(() => ArrangerAssert(async () => await gt.ExpectationsArranger.AssertAsync(null, null)));
            Assert.AreEqual("Expected an exception; however, the execution was successful.", ex.Message);

            ArrangerAssert(async () => await gt.ExpectationsArranger.AssertAsync(null, new DivideByZeroException()));
        }

        [Test]
        public void ExceptionSuccess_ExpectException_Message()
        {
            var gt = GenericTester.Create().ExpectException("error");
            ArrangerAssert(async () => await gt.ExpectationsArranger.AssertAsync(null, new DivideByZeroException("error")));
            ArrangerAssert(async () => await gt.ExpectationsArranger.AssertAsync(null, new DivideByZeroException("Error")));

            var ex = Assert.Throws<Exception>(() => ArrangerAssert(async () => await gt.ExpectationsArranger.AssertAsync(null, new DivideByZeroException("not ok"))));
            Assert.AreEqual("Expected Exception message 'error' is not contained within 'not ok'.", ex.Message);
        }

        [Test]
        public void ExceptionSuccess_ExpectException_Type()
        {
            var gt = GenericTester.Create().ExpectException().Type<DivideByZeroException>();

            var ex = Assert.Throws<Exception>(() => ArrangerAssert(async () => await gt.ExpectationsArranger.AssertAsync(null, null)));
            Assert.AreEqual("Expected an exception; however, the execution was successful.", ex.Message);

            ex = Assert.Throws<Exception>(() => ArrangerAssert(async () => await gt.ExpectationsArranger.AssertAsync(null, new NotSupportedException())));
            Assert.AreEqual("Expected Exception type 'DivideByZeroException' not equal to actual 'NotSupportedException'.", ex.Message);

            ArrangerAssert(async () => await gt.ExpectationsArranger.AssertAsync(null, new DivideByZeroException()));
        }

        [Test]
        public void ExpectError_None()
        {
            var gt = GenericTester.Create().ExpectError("No error will be raised.");

            var ex = Assert.Throws<Exception>(() => ArrangerAssert(async () => await gt.ExpectationsArranger.AssertAsync(null, null)));
            Assert.AreEqual("Expected one or more errors; however, none were returned.", ex.Message);
        }

        [Test]
        public void ExpectValue_Simple()
        {
            var gt = GenericTester.CreateFor<string>().ExpectValue("bob");
            ArrangerAssert(async () => await gt.ExpectationsArranger.AssertValueAsync(null, "bob"));

            var ex = Assert.Throws<Exception>(() => ArrangerAssert(async () => await gt.ExpectationsArranger.AssertValueAsync(null, "jenny")));
            Assert.IsTrue(ex.Message.Contains("Value is not equal: \"bob\" != \"jenny\"."));

            ex = Assert.Throws<Exception>(() => ArrangerAssert(async () => await gt.ExpectationsArranger.AssertValueAsync(null, null)));
            Assert.IsTrue(ex.Message.Contains("Kind is not equal: String != Null."));
        }

        [Test]
        public void ExpectValue_WithFunc()
        { 
            var gt = GenericTester.CreateFor<string>().ExpectValue(_ => "bob");
            ArrangerAssert(async () => await gt.ExpectationsArranger.AssertValueAsync(null, "bob"));

            var ex = Assert.Throws<Exception>(() => ArrangerAssert(async () => await gt.ExpectationsArranger.AssertValueAsync(null, "jenny")));
            Assert.IsTrue(ex.Message.Contains("Value is not equal: \"bob\" != \"jenny\"."));

            ex = Assert.Throws<Exception>(() => ArrangerAssert(async () => await gt.ExpectationsArranger.AssertValueAsync(null, null)));
            Assert.IsTrue(ex.Message.Contains("Kind is not equal: String != Null."));
        }

        [Test]
        public void ExpectValueComplex()
        {
            var gt = GenericTester.CreateFor<Entity<int>>().ExpectValue(new Entity<int> { Id = 88, Name = "bob" });

            ArrangerAssert(async () => await gt.ExpectationsArranger.AssertValueAsync(null, new Entity<int> { Id = 88, Name = "bob" }));
            ArrangerAssert(async () => await gt.ExpectationsArranger.AssertValueAsync(null, new { Id = 88, Name = "bob" }));

            var ex = Assert.Throws<Exception>(() => ArrangerAssert(async () => await gt.ExpectationsArranger.AssertValueAsync(null, new { Id = 99, Name = "bob" })));
            Assert.IsTrue(ex.Message.Contains("Path '$.id': Value is not equal: 88 != 99."));

            gt = GenericTester.CreateFor<Entity<int>>().ExpectValue(new Entity<int> { Id = 88, Name = "bob" }, "id");
            ArrangerAssert(async () => await gt.ExpectationsArranger.AssertValueAsync(null, new { Id = 99, Name = "bob" }));
        }

        public class Entity<TId>
        {
            public TId Id { get; set; }

            public string Name { get; set; }
        }

        private static void ArrangerAssert(Func<Task> func)
        {
            try
            {
                var t = Task.Run(() => func());
                t.Wait();
            }
            catch (AggregateException agex) when (agex.InnerException is not null && agex.InnerException is AssertionException aex)
            {
                throw new Exception(aex.Message, aex);
            }
            catch (AssertionException aex)
            {
                throw new Exception(aex.Message, aex);
            }
        }
    }
}