using MongoDB.Driver;

namespace Kanban.Repository
{
    public class DBUpgrater
    {
        private const string EstimatedWorkEffort_Name = "EstimatedWorkEffort";
        private DBVersion m_DBVersion;
        private IDBCard m_DBCards;

        public DBUpgrater(DBVersion dbVersion, IDBCard dbCards)
        {
            m_DBVersion = dbVersion;
            m_DBCards = dbCards;
        }

        public async Task UpgrateDB()
        {
            Version version = await m_DBVersion.GetDBVersion();
            if (CanUpgradeTo1_1_0(version))
            {
                await UpgradeStoryPoints();
                await UpgradeVersion(version.UpgradeMinor());
            }
            else if (CanUpgradeTo1_2_0(version))
            {
                DeleteCardField(EstimatedWorkEffort_Name);
                await UpgradeVersion(version.UpgradeMinor());
            }
            else
            {
                if (ExistCardField(EstimatedWorkEffort_Name))
                    DeleteCardField(EstimatedWorkEffort_Name);
            }
        }

        private bool CanUpgradeTo1_1_0(Version version) =>
            ((version.Major == 1) && (version.Minor == 0) && (version.Revision == 0));

        private bool CanUpgradeTo1_2_0(Version version) =>
            ((version.Major == 1) && (version.Minor == 1));

        private async Task UpgradeStoryPoints()
        {
            var estimatedWorkEfforts = Enum.GetValues(typeof(EstimatedWorkEffort));
            foreach (var estimatedWorkEffort in estimatedWorkEfforts)
            {
                var filter = Builders<Card>.Filter.Eq(EstimatedWorkEffort_Name, estimatedWorkEffort);
                var update = Builders<Card>.Update.Set(nameof(Card.StoryPoints), DefaultStoryPoints((EstimatedWorkEffort)estimatedWorkEffort));
                await m_DBCards.CardsCollection.UpdateManyAsync(filter, update);
            }
        }

        /// <summary>
        /// Check if the given field exists in Card collection
        /// </summary>
        /// <param name="fieldName">field name of Card collection</param>
        /// <returns>true if exists</returns>
        private bool ExistCardField(string fieldName)
        {
            var filter = Builders<Card>.Filter.Exists(fieldName, exists: true);
            var count = m_DBCards.CardsCollection.Find(filter).CountDocuments();
            return count > 0;
        }

        private void DeleteCardField(string propertyName)
        {
            var filter = Builders<Card>.Filter.Empty;
            var update = Builders<Card>.Update.Unset(propertyName);
            m_DBCards.CardsCollection.UpdateMany(filter, update);
        }

        private int DefaultStoryPoints(EstimatedWorkEffort estimatedWorkEffort)
        {
            switch (estimatedWorkEffort)
            {
                case EstimatedWorkEffort.Small:
                    return 2;
                case EstimatedWorkEffort.Medium:
                    return 5;
                case EstimatedWorkEffort.Large:
                    return 8;
                case EstimatedWorkEffort.LargeExtra:
                    return 13;
                default:
                    return Card.UNKNOWN_STORY_POINTS; // Unknown
            }
        }

        private async Task<UpdateResult> UpgradeVersion(Version version) =>
            await m_DBVersion.UpdateAsync(version);
    }
}
