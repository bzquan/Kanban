using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Kanban.Repository
{
    [BsonIgnoreExtraElements]   // Ignore any extra fields instead of throwing an exception
    public class Card : KanbanItem
    {
        public const int UNKNOWN_STORY_POINTS = 999;

        public ObjectId BoardID { get; set; }

        public string Tag { get; set; } = "";
        public string Executors { get; set; } = "";

        public string Summary { get; set; } = "";
        public string TestCases { get; set; } = "";
        public string DesignOverview { get; set; } = "";

        public CardType CardType { get; set; } = CardType.Feature;

        public int StoryPoints { get; set; } = UNKNOWN_STORY_POINTS;

        public WorkState WorkState { get; set; } = new WorkState();

        public DateTime CreateDate { get; set; } = DateTime.Now;

        public DateTime StateChangedDate { get; set; } = DateTime.Now;

        public DateTime ReleaseDate { get; set; } = DateTime.Now.AddDays(1);

        public bool IsBlocked { get; set; } = false;
        public bool IsMergedIntoMaster { get; set; } = false;
        public bool IsMergedIntoMajorBranch { get; set; } = false;
        public bool IsScheduledToRelease { get; set; } = false;

        public bool IsOnBackBoard { get; set; } = false;
        public DateTime OnBackBoardDate { get; set; } = DateTime.Now;
        public void ConvertLocalTime()
        {
            CreateDate = CreateDate.UTC2Local();
            StateChangedDate = StateChangedDate.UTC2Local();
            ReleaseDate = ReleaseDate.UTC2Local();
            OnBackBoardDate = OnBackBoardDate.UTC2Local();
        }
    }
}
