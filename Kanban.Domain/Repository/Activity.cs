using System;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Kanban.Repository
{
    [BsonIgnoreExtraElements]   // Ignore any extra fields instead of throwing an exception
    public class Activity
    {
        public ObjectId _id { get; set; }
        public ObjectId BoardID { get; set; }
        public ObjectId CardID { get; set; }
        public WorkState WorkState { get; set; }
        public DateTime StateChangedDate { get; set; }
        public void ConvertLocalTime() => StateChangedDate = StateChangedDate.UTC2Local();
    }
}
