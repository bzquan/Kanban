using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Bson;

namespace Kanban.Model
{
    public interface IActivityRepository
    {
        Task Insert(Activity activity);
        Task<List<Activity>> GetActivitiesOf(Card card);
        Task<List<Activity>> GetActivitiesOf(List<ObjectId> cardIDs);
        Task<DeleteResult> DeleteActivitiesOf(Card board);
        Task<DeleteResult> DeleteActivitiesOf(ObjectId card_id);
    }
}
