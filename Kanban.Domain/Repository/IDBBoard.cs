using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver;
using System.Runtime.CompilerServices;
using MongoDB.Bson;

namespace Kanban.Repository
{
    public interface IDBBoard
    {
        Task<Board> CreateNewBord();
        Task Insert(Board board);
        Task Insert(List<Board> boards);
        Task<List<Board>> GetBoards();
        Task<ReplaceOneResult> Save(Board board);
        UpdateResult Update<T>(Board board, string propertyName, T propertyValue);
        Task<UpdateResult> UpdateAsync<T>(Board board, string propertyName, T propertyValue);
        UpdateResult MoveCard(Board board, int fromSeqNo, int toSeqNo, bool forward);
        UpdateResult ChangeCardsAndActivitySeqNo(Board board, int fromSeqNo, int toSeqNo);
        Task<DeleteResult> Delete(ObjectId board_id);
        void DropBoards();
    }
}
