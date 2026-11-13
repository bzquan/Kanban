using MongoDB.Bson;

namespace Kanban.Services;

/// <summary>
/// ボード変更通知の口(設計書§4.4)。書き込みエンドポイントの成功経路末でのみ呼ばれる。
/// サーバーは NoOp を既定とし、将来の SignalR 実装はこの差し替えだけで済む。
/// </summary>
public interface IBoardChangeNotifier
{
    void BoardChanged(ObjectId boardId);
}
