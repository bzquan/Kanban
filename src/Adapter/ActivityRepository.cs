using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;

using MongoDB.Bson;

namespace Kanban.Infrastructure
{
    public class ActivityRepository : Model.IActivityRepository
    {
        Repository.IDBActivity m_DBActivity;

        public ActivityRepository(Repository.IDBActivity dbActivies)
        {
            m_DBActivity = dbActivies;
        }

        public async Task Insert(Model.Activity activity) => await m_DBActivity.Insert(activity.ActivityOfRepository);

        public async Task<DeleteResult> DeleteActivitiesOf(Model.Card card)
        {
            return await DeleteActivitiesOf(card._id);
        }

        public async Task<DeleteResult> DeleteActivitiesOf(ObjectId card_id)
        {
            return await m_DBActivity.DeleteActivitiesOf(card_id);
        }

        public async Task<List<Model.Activity>> GetActivitiesOf(Model.Card card)
        {
            List<Repository.Activity> activities = await m_DBActivity.GetActivitiesOf(card.CardOfRepository);
            return RepositoryActivities2Model(activities);
        }

        public async Task<List<Model.Activity>> GetActivitiesOf(List<ObjectId> cardIDs)
        {
            List<Repository.Activity> activities = await m_DBActivity.GetActivitiesOf(cardIDs);
            return RepositoryActivities2Model(activities);
        }

        private static List<Model.Activity> RepositoryActivities2Model(List<Repository.Activity> activities)
        {
            List<Model.Activity> activityList = new List<Model.Activity>();
            activities.ForEach(activity => activityList.Add(new Model.Activity { ActivityOfRepository = activity }));
            return activityList;
        }
    }
}
