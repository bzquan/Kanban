using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using static Kanban.Util.Util;

namespace UnitTest
{
    [TestClass]
    public class ProgramExistTest
    {
        [TestMethod]
        public void TestMongoDumpExists()
        {
            // Givn
            string program = "mongodump.exe";

            // When
            bool exist = ExistsOnPath(program);

            // Then
            Assert.IsTrue(exist);
        }

        [TestMethod]
        public void TestMongoRestoreExists()
        {
            // Givn
            string program = "mongorestore.exe";

            // When
            bool exist = ExistsOnPath(program);

            // Then
            Assert.IsTrue(exist);
        }
    }
}
