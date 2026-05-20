using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace Kanban.Infrastructure
{
    public class BoardRepository : Model.IBoardRepository
    {
        Repository.IDBBoard m_DBBoards;
        Repository.IDBActivity m_DBActivies;
        Model.IBoardFactory m_BoardFactory;

        public BoardRepository(Repository.IDBBoard dbBoards, Model.IBoardFactory boardFactory, Repository.IDBActivity dbActivies)
        {
            m_DBBoards = dbBoards;
            m_BoardFactory = boardFactory;
            m_DBActivies = dbActivies;
        }

        public async Task<List<Model.Board>> LoadBoards()
        {
            List<Repository.Board> boards = await m_DBBoards.GetBoards();
            if (boards.Count == 0)
            {
                // 新規看板を生成して、DBに登録する
                await m_BoardFactory.CreateBoard();
                boards = await m_DBBoards.GetBoards();
            }

            List<Model.Board> loadedBoards = new List<Model.Board>();
            boards.ForEach(board => loadedBoards.Add(new Model.Board(board)));
            loadedBoards.ForEach(board => board.SetDefaultCycleTimeDuration());

            return loadedBoards;
        }

        public UpdateResult Update<T>(Model.Board board, string propertyName, T propertyValue)
        {
            return m_DBBoards.Update(board.BoardOfRepository, propertyName, propertyValue);
        }

        public async Task<UpdateResult> UpdateAsync<T>(Model.Board board, string propertyName, T propertyValue)
        {
            return await m_DBBoards.UpdateAsync(board.BoardOfRepository, propertyName, propertyValue);
        }

        public UpdateResult MoveCard(Model.Board board, int fromSeqNo, int toSeqNo, bool forward)
        {
            return m_DBBoards.MoveCard(board.BoardOfRepository, fromSeqNo, toSeqNo, forward);
        }

        public UpdateResult ChangeCardsAndActivitySeqNo(Model.Board board, int fromSeqNo, int toSeqNo)
        {
            return m_DBBoards.ChangeCardsAndActivitySeqNo(board.BoardOfRepository, fromSeqNo, toSeqNo);
        }

        public async Task<List<Model.Activity>> GetActivities4Metrics(Model.Board board, DateTime from, DateTime to)
        {
            List<Repository.Activity> activities = await m_DBActivies.GetActivities4Metrics(board.BoardOfRepository, from, to);
            List<Model.Activity> activityList = new List<Model.Activity>();
            activities.ForEach(activity => activityList.Add(new Model.Activity { ActivityOfRepository = activity }));
            return activityList;

        }

        public async Task<DeleteResult> DeleteActivities(Model.Board board, int seqNo)
        {
            return await m_DBActivies.DeleteActivities(board.BoardOfRepository, seqNo);
        }

        public async Task<ReplaceOneResult> Save(Model.Board board)
        {
            return await m_DBBoards.Save(board.BoardOfRepository);
        }

        public async Task<DeleteResult> Delete(Model.Board board)
        {
            return await m_DBBoards.Delete(board._id);
        }
    }
}
