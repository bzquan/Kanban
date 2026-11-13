namespace UnitTest;

/// <summary>
/// TestKanban が接続してよいDB名。localhost:27017 は共有リモート Mongo へのポートフォワードであり、
/// 自動テストが触ってよいDBは kanban_test 系のみ(設計書§4.7・計画 Global Constraints)。
/// Api.Tests の kanban_test とは別名にして2スイートが互いを潰さない。
/// </summary>
public static class TestDb
{
    public const string Name = "kanban_test_wpf";
}
