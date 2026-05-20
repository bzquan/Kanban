using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace Kanban.Repository
{
    [BsonIgnoreExtraElements]   // Ignore any extra fields instead of throwing an exception
    public class Board : KanbanItem
    {
        public Board(string title = "新規ボード", string description = "詳細情報")
        {
            base.Title = title;
            Description = description;
        }
        public string Description { get; set; }
        public List<ProcessStep> ProcessSteps { get; set; }
    }
}
