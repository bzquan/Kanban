using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanban.Repository
{
    public class DBVersion
    {
        IDBClient m_DBClient;

        public DBVersion(IDBClient dbClient)
        {
            m_DBClient = dbClient;
        }

        public async Task<Version> GetDBVersion()
        {
            List<Version> versions = await VersionCollection
                                    .Find(new BsonDocument())
                                    .ToListAsync();
            if (versions.Count == 0)
            {
                Version version = new Version();
                await Insert(version);
                return version;
            }
            else
                return versions[0];
        }

        public async Task Insert(Version version)
            => await VersionCollection.InsertOneAsync(version);

        public async Task<UpdateResult> UpdateAsync(Version version)
        {
            await UpdateAsync(nameof(version.Major), version.Major);
            await UpdateAsync(nameof(version.Minor), version.Minor);
            return await UpdateAsync(nameof(version.Revision), version.Revision);
        }

        private async Task<UpdateResult> UpdateAsync(string propertyName, int value)
        {
            var filter = Builders<Version>.Filter.Empty;
            var update = Builders<Version>.Update.Set(propertyName, value);
            return await VersionCollection.UpdateManyAsync(filter, update);
        }

        private IMongoCollection<Version> VersionCollection =>
            m_DBClient.DB.GetCollection<Version>(CollectionNames.VersionCollectionName);
    }
}
