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
            Assert.That(ex.Message, Is.EqualTo("Expected an exception; however, the execution was successful."));

            gt.ExpectException().Any();
            ArrangerAssert(async () => await gt.ExpectationsArranger.AssertAsync(null, new DivideByZeroException()));
        }

        [Test]
        public void ExceptionSuccess_ExpectException_Message()
        {
            var gt = GenericTester.Create().ExpectException("error");
            ArrangerAssert(async () => await gt.ExpectationsArranger.AssertAsync(null, new DivideByZeroException("error")));

            gt.ExpectException("error");
            ArrangerAssert(async () => await gt.ExpectationsArranger.AssertAsync(null, new DivideByZeroException("Error")));

            gt.ExpectException("error");
            var ex = Assert.Throws<Exception>(() => ArrangerAssert(async () => await gt.ExpectationsArranger.AssertAsync(null, new DivideByZeroException("not ok"))));
            Assert.That(ex.Message, Is.EqualTo("Expected Exception message 'error' is not contained within 'not ok'."));
        }

        [Test]
        public void ExceptionSuccess_ExpectException_Type()
        {
            var gt = GenericTester.Create().ExpectException().Type<DivideByZeroException>();
            var ex = Assert.Throws<Exception>(() => ArrangerAssert(async () => await gt.ExpectationsArranger.AssertAsync(null, null)));
            Assert.That(ex.Message, Is.EqualTo("Expected an exception; however, the execution was successful."));

            gt.ExpectException().Type<DivideByZeroException>();
            ex = Assert.Throws<Exception>(() => ArrangerAssert(async () => await gt.ExpectationsArranger.AssertAsync(null, new NotSupportedException())));
            Assert.That(ex.Message, Is.EqualTo("Expected Exception type 'DivideByZeroException' not equal to actual 'NotSupportedException'."));

            gt.ExpectException().Type<DivideByZeroException>();
            ArrangerAssert(async () => await gt.ExpectationsArranger.AssertAsync(null, new DivideByZeroException()));
        }

        [Test]
        public void ExpectError_None()
        {
            var gt = GenericTester.Create().ExpectError("No error will be raised.");
            var ex = Assert.Throws<Exception>(() => ArrangerAssert(async () => await gt.ExpectationsArranger.AssertAsync(null, null)));
            Assert.That(ex.Message, Is.EqualTo("Expected one or more errors; however, none were returned."));
        }

        [Test]
        public void ExpectValue_Simple()
        {
            var gt = GenericTester.CreateFor<string>().ExpectValue("bob");
            ArrangerAssert(async () => await gt.ExpectationsArranger.AssertValueAsync(null, "bob"));

            gt.ExpectValue("bob");
            var ex = Assert.Throws<Exception>(() => ArrangerAssert(async () => await gt.ExpectationsArranger.AssertValueAsync(null, "jenny")));
            Assert.That(ex.Message, Does.Contain("Value is not equal: \"bob\" != \"jenny\"."));

            gt.ExpectValue("bob");
            ex = Assert.Throws<Exception>(() => ArrangerAssert(async () => await gt.ExpectationsArranger.AssertValueAsync(null, null)));
            Assert.That(ex.Message, Does.Contain("Kind is not equal: String != Null."));
        }

        [Test]
        public void ExpectValue_WithFunc()
        { 
            var gt = GenericTester.CreateFor<string>().ExpectValue(_ => "bob");
            ArrangerAssert(async () => await gt.ExpectationsArranger.AssertValueAsync(null, "bob"));

            gt.ExpectValue(_ => "bob");
            var ex = Assert.Throws<Exception>(() => ArrangerAssert(async () => await gt.ExpectationsArranger.AssertValueAsync(null, "jenny")));
            Assert.That(ex.Message, Does.Contain("Value is not equal: \"bob\" != \"jenny\"."));

            gt.ExpectValue(_ => "bob");
            ex = Assert.Throws<Exception>(() => ArrangerAssert(async () => await gt.ExpectationsArranger.AssertValueAsync(null, null)));
            Assert.That(ex.Message, Does.Contain("Kind is not equal: String != Null."));
        }

        [Test]
        public void ExpectValueComplex()
        {
            var gt = GenericTester.CreateFor<Entity<int>>().ExpectValue(new Entity<int> { Id = 88, Name = "bob" });
            ArrangerAssert(async () => await gt.ExpectationsArranger.AssertValueAsync(null, new Entity<int> { Id = 88, Name = "bob" }));

            gt.ExpectValue(new Entity<int> { Id = 88, Name = "bob" });
            ArrangerAssert(async () => await gt.ExpectationsArranger.AssertValueAsync(null, new { Id = 88, Name = "bob" }));

            gt.ExpectValue(new Entity<int> { Id = 88, Name = "bob" });
            var ex = Assert.Throws<Exception>(() => ArrangerAssert(async () => await gt.ExpectationsArranger.AssertValueAsync(null, new { Id = 99, Name = "bob" })));
            Assert.That(ex.Message, Does.Contain("Path '$.id': Value is not equal: 88 != 99."));

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