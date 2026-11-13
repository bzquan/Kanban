namespace UnitTest;

/// <summary>
/// 安全性不変条件の回帰テスト: TestKanban の全フィクスチャが Drop/再接続するDB名は
/// kanban_test 系でなければならない(共有フォワード先サーバに別名のDBを作らない)。
/// </summary>
[TestClass]
public class DbNameSafetyTest
{
    [TestMethod]
    public void TestDbName_MustBelongToKanbanTestFamily()
    {
        StringAssert.StartsWith(TestDb.Name, "kanban_test",
            "TestKanban may only touch DBs in the kanban_test family (shared forwarded Mongo — Global Constraints)");
        Assert.AreNotEqual("kanban", TestDb.Name, "production DB name must never be used by automation");
    }
}
