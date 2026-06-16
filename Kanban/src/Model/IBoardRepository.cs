using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace Kanban.Model
{
    public interface IBoardRepository
    {
        Task<List<Board>> LoadBoards();
        UpdateResult Update<T>(Board board, string propertyName, T propertyValue);
        Task<UpdateResult> UpdateAsync<T>(Board board, string propertyName, T propertyValue);
        UpdateResult MoveCard(Board board, int fromSeqNo, int toSeqNo, bool forward);
        UpdateResult ChangeCardsAndActivitySeqNo(Board board, int fromSeqNo, int toSeqNo);
        Task<List<Activity>> GetActivities4Metrics(Board board, DateTime from, DateTime to);
        Task<DeleteResult> DeleteActivities(Board board, int seqNo);
        Task<ReplaceOneResult> Save(Board board);
        Task<DeleteResult> Delete(Board board);
    }
}
