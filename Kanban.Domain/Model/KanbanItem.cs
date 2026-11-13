
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Runtime.CompilerServices;

namespace Kanban.Model
{
    public abstract class KanbanItem
    {
        private Repository.KanbanItem m_Item;

        public KanbanItem(Repository.KanbanItem item)
        {
            m_Item = item;
        }

        [BsonId]
        public ObjectId _id
        {
            get { return m_Item._id; }
        }

        public int SeqNo
        {
            get { return m_Item.SeqNo; }
            set
            {
                m_Item.SeqNo = value;
                UpdateProperty(value);
            }
        }

        public string Title
        {
            get { return m_Item.Title; }
            set
            {
                m_Item.Title = value;
                UpdateProperty(value);
            }
        }

        public void UpdateProperty<T>(T newValue, [CallerMemberName] string propertyName = "")
        {
            Update(newValue, propertyName);
        }

        protected abstract void Update<T>(T propertyValue, [CallerMemberName] string propertyName = "");
    }
}
