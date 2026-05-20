using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanban.Repository
{
    public class Version
    {
        public Version(int major = 1, int minor = 0, int revision = 0)
        {
            Major = major;
            Minor = minor;
            Revision = revision;
        }

        public ObjectId _id { get; set; }

        public int Major { get; set; }
        public int Minor { get; set; }
        public int Revision { get; set; }

        public Version UpgradeMinor() => new Version(Major, Minor + 1, revision: 0);
    }
}
