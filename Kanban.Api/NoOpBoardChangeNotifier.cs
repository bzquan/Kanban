using Kanban.Services;
using MongoDB.Bson;

namespace Kanban.Api;

/// <summary>既定の実装(何もしない)。SignalR 実装は DI の差し替えで導入する。</summary>
public class NoOpBoardChangeNotifier : IBoardChangeNotifier
{
    public void BoardChanged(ObjectId boardId)
    {
    }
}
