using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTestEx.Json;

namespace UnitTestEx.MSTest.Test.Other
{
    [TestClass]
    public class JsonElementComparerTest
    {
        [TestMethod]
        public void Compare_Object_SameSame()
        {
            Assert.IsTrue(new JsonElementComparer().Compare("{\"name\":\"gary\",\"age\":40.0,\"cool\":false,\"salary\":null}", "{\"name\":\"gary\",\"age\":40.0,\"cool\":false,\"salary\":null}").AreEqual);
        }

        [TestMethod]
        public void Compare_Object_DiffOrderAndDiffNumberFormat()
        {
            Assert.IsTrue(new JsonElementComparer().Compare("{\"name\":\"gary\",\"cool\":false,\"age\":40,\"salary\":null}", "{\"name\":\"gary\",\"age\":40.0,\"cool\":false,\"salary\":null}").AreEqual);
        }

        [TestMethod]
        public void Compare_Object_DiffValuesAndTypes()
        {
            Assert.AreEqual(@"Path '$.name': Value is not equal: ""gary"" != ""brian"".
Path '$.age': Value is not equal: 40.0 != 41.0.
Path '$.cool': Kind is not equal: Null != False.
Path '$.salary': Kind is not equal: Number != Null.",
                new JsonElementComparer().Compare("{\"name\":\"gary\",\"age\":40.0,\"cool\":null,\"salary\":42000}", "{\"name\":\"brian\",\"age\":41.0,\"cool\":false,\"salary\":null}").ToString());
        }

        [TestMethod]
        public void Compare_Object_PropertyNameMismatch()
        {
            Assert.AreEqual(@"Path '$.name': Does not exist in right JSON.
Path '$.salary': Does not exist in right JSON.
Path '$.Name': Does not exist in left JSON.",
                new JsonElementComparer().Compare("{\"name\":\"gary\",\"age\":40.0,\"cool\":false,\"salary\":null}", "{\"Name\":\"gary\",\"age\":40.0,\"cool\":false}").ToString());
        }

        [TestMethod]
        public void Compare_Object_PropertyNameMismatch_Exclude()
        {
            Assert.AreEqual("Path '$.salary': Does not exist in right JSON.",
                new JsonElementComparer().Compare("{\"name\":\"gary\",\"age\":40.0,\"cool\":false,\"salary\":null}", "{\"Name\":\"gary\",\"age\":40.0,\"cool\":false}", "name", "Name").ToString());
        }

        [TestMethod]
        public void Compare_Array_LengthMismatch()
        {
            Assert.AreEqual("Path '$': Array lengths are not equal: 3 != 4.",
                new JsonElementComparer().Compare("[1,2,3]", "[1,2,3,4]").ToString());
        }

        [TestMethod]
        public void Compare_Array_ItemMismatch()
        {
            Assert.AreEqual("Path '$[3]': Value is not equal: 5 != 4.",
                new JsonElementComparer().Compare("[1,2,3,5]", "[1,2,3,4]").ToString());
        }

        [TestMethod]
        public void Compare_Object_Array_ItemMismatch()
        {
            Assert.AreEqual(@"Path '$.names[1].name': Value is not equal: ""brian"" != ""rebecca"".",
                new JsonElementComparer().Compare("{\"names\":[{\"name\":\"gary\"},{\"name\":\"brian\"}]}", "{\"names\":[{\"name\":\"gary\"},{\"name\":\"rebecca\"}]}").ToString());
        }

        [TestMethod]
        public void Compare_Object_Array_Exclude()
        {
            Assert.IsTrue(new JsonElementComparer().Compare("{\"names\":[{\"name\":\"gary\"},{\"name\":\"brian\"}]}", "{\"names\":[{\"name\":\"gary\"},{\"name\":\"rebecca\"}]}", "names.name").AreEqual);
        }

        [TestMethod]
        public void Compare_Object_Array_ItemMismatchComplex()
        {
            Assert.AreEqual(@"Path '$.names[0].address.street': Value is not equal: 1 != 2.",
                new JsonElementComparer().Compare("{\"names\":[{\"name\":\"gary\",\"address\":{\"street\": 1}},{\"name\":\"brian\"}]}", "{\"names\":[{\"name\":\"gary\",\"address\":{\"street\": 2}},{\"name\":\"brian\"}]}").ToString());
        }

        [TestMethod]
        public void Equals_Object_SameSame()
        {
            Assert.IsTrue(new JsonElementComparer().Equals("{\"name\":\"gary\",\"age\":40.0,\"cool\":false,\"salary\":null}", "{\"name\":\"gary\",\"age\":40.0,\"cool\":false,\"salary\":null}"));
        }

        [TestMethod]
        public void Equals_Object_DiffOrderAndDiffNumberFormat()
        {
            Assert.IsTrue(new JsonElementComparer().Equals("{\"name\":\"gary\",\"cool\":false,\"age\":40,\"salary\":null}", "{\"name\":\"gary\",\"age\":40.0,\"cool\":false,\"salary\":null}"));
        }

        [TestMethod]
        public void Equals_Object_DiffValuesAndTypes()
        {
            Assert.IsFalse(new JsonElementComparer().Equals("{\"name\":\"gary\",\"age\":40.0,\"cool\":null,\"salary\":42000}", "{\"name\":\"brian\",\"age\":41.0,\"cool\":false,\"salary\":null}"));
        }

        [TestMethod]
        public void Equals_Object_PropertyNameMismatch()
        {
            Assert.IsFalse(new JsonElementComparer().Equals("{\"name\":\"gary\",\"age\":40.0,\"cool\":false,\"salary\":null}", "{\"Name\":\"gary\",\"age\":40.0,\"cool\":false}"));
        }

        [TestMethod]
        public void Equals_Array_LengthMismatch()
        {
            Assert.IsFalse(new JsonElementComparer().Equals("[1,2,3]", "[1,2,3,4]"));
        }

        [TestMethod]
        public void Equals_Array_ItemMismatch()
        {
            Assert.IsFalse(new JsonElementComparer().Equals("[1,2,3,5]", "[1,2,3,4]"));
        }

        [TestMethod]
        public void Equals_Object_Array_ItemMismatch()
        {
            Assert.IsFalse(new JsonElementComparer().Equals("{\"names\":[{\"name\":\"gary\"},{\"name\":\"brian\"}]}", "{\"names\":[{\"name\":\"gary\"},{\"name\":\"rebecca\"}]}"));
        }

        [TestMethod]
        public void Equals_Object_Array_ItemMismatchComplex()
        {
            Assert.IsFalse(new JsonElementComparer().Equals("{\"names\":[{\"name\":\"gary\",\"address\":{\"street\": 1}},{\"name\":\"brian\"}]}", "{\"names\":[{\"name\":\"gary\",\"address\":{\"street\": 2}},{\"name\":\"brian\"}]}"));
        }

        [TestMethod]
        public void Hashcode_Object_DiffOrderAndDiffNumberFormat()
        {
            Assert.AreEqual(new JsonElementComparer().GetHashCode("{\"name\":\"gary\",\"cool\":false,\"age\":40,\"salary\":null}"), new JsonElementComparer().GetHashCode("{\"name\":\"gary\",\"age\":40.0,\"cool\":false,\"salary\":null}"));
            Assert.AreNotEqual(new JsonElementComparer().GetHashCode("{\"names\":[{\"name\":\"gary\",\"address\":{\"street\": 1}},{\"name\":\"brian\"}]}"), new JsonElementComparer().GetHashCode("{\"names\":[{\"name\":\"gary\",\"address\":{\"street\": 2}},{\"name\":\"brian\"}]}"));
        }
    }
}