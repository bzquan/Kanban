using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Bson;

namespace Kanban.Repository
{
    public interface IDBActivity
    {
        Task Insert(Activity activity);
        Task<List<Activity>> GetActivitiesOf(Card card);
        Task<List<Activity>> GetActivitiesOf(List<ObjectId> cardIDs);
        Task<List<Activity>> GetActivities4Metrics(Board board, DateTime from, DateTime to);
        Task<DeleteResult> DeleteActivities(Board board, int seqNo);
        Task<DeleteResult> DeleteActivities(ObjectId board_id);
        Task<DeleteResult> DeleteActivitiesOf(Card board);
        Task<DeleteResult> DeleteActivitiesOf(ObjectId card_id);
        UpdateResult UpdateActivities(Board board, int fromSeqNo, int toSeqNo, bool forward);
        UpdateResult ChangeActivitiesSeqNo(Board board, int fromSeqNo, int toSeqNo);
        void DropActivities();
    }
}
