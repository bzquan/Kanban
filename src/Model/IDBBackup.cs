namespace Kanban.Model
{
    public interface IDBBackup
    {
        bool HaveDBBackupTools();
        void DumpDB(string dst_folder);
        void RestoreDB(string src_folder, Util.DBPriority4Restore db_priority);
    }
}
