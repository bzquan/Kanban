using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Kanban.Util;
using static Kanban.Util.KanbanDefinitions;
using static Kanban.Util.Util;

namespace Kanban.Infrastructure
{
    public delegate IProcessExecutor ProcessExecutorFactory();

    public class DBBackup : Model.IDBBackup
    {
        ProcessExecutorFactory m_ProcessExecutorFactory;
        Repository.IDBClient m_DBClient;
        ProcessExecuteCompositeCommand m_ProcessExecuteCompositeCommand;

        public DBBackup(ProcessExecutorFactory processExecutorFactory, Model.IProcessExecutorClient processExecutorClient, Repository.IDBClient dbClient)
        {
            m_ProcessExecutorFactory = processExecutorFactory;
            m_DBClient = dbClient;
            m_ProcessExecuteCompositeCommand = new ProcessExecuteCompositeCommand(processExecutorClient);
        }

        private bool CommandStarted { get; set; } = false;

        public void DumpDB(string dst_folder)
        {
            string param = DBHost + "--out " + dst_folder;
            EnqueueCammand(MongoDBDumpToolPath, param);
            m_ProcessExecuteCompositeCommand.Execute();
        }

        private void EnqueueCammand(string programPath, string param)
        {
            ProcessArgument argument = new ProcessArgument { ProgramPath = programPath, Arguments = param };
            IProcessExecutor processExecutor = m_ProcessExecutorFactory();
            ProcessExecuteCommand command = new ProcessExecuteCommand(processExecutor, argument, m_ProcessExecuteCompositeCommand);
            m_ProcessExecuteCompositeCommand.EnqueueCammand(command);
        }

        public bool HaveDBBackupTools()
        {
            if (ExistOnStartupFolder(MongoDumpTool, MongoRestoreTool)) return true;

            string tool_path = ConfigReader.GetValue("DB_BACKUP_TOOL_PATH", "\\");
            if (ExistOnFolder(tool_path, MongoDumpTool, MongoRestoreTool)) return true;

            // Check if backup tools exist on a folader of PATH environment variables
            return ExistsOnPath(MongoDumpTool, MongoRestoreTool);
        }

        private string MongoDBDumpToolPath
        {
            get
            {
                if (ExistOnStartupFolder(MongoDumpTool))
                {
                    return System.IO.Path.Combine(StartupFolder(), MongoDumpTool);
                }

                string tool_path = ConfigReader.GetValue("DB_BACKUP_TOOL_PATH", "\\");
                if (ExistOnFolder(tool_path, MongoDumpTool))
                {
                    return System.IO.Path.Combine(tool_path, MongoDumpTool);
                }

                return MongoDumpTool;
            }
        }

        private string DBHost => "--host " + m_DBClient.DBHost + " ";

        public void RestoreDB(string src_folder, DBPriority4Restore db_priority)
        {
            if (db_priority == DBPriority4Restore.ToBeRestoredDB)
            {
                CreateRestoreCommands4ToBeRestoredDB(src_folder);
            }
            else
            {
                CreateRestoreCommands4CurrentDB(src_folder, db_priority);
            }

            m_ProcessExecuteCompositeCommand.Execute();
        }

        private string TempFolder => System.IO.Path.Combine(StartupFolder(), "tempdb");

        private void CreateRestoreCommands4ToBeRestoredDB(string src_folder)
        {
            CreateBackupDBTemporarily();
            RestoreSrcDB(src_folder);
            RestoreTemprorilyBackedupDB();
        }

        private void CreateBackupDBTemporarily()
        {
            string param = DBHost + "--out " + TempFolder;
            EnqueueCammand(MongoDBDumpToolPath, param);
        }

        private void RestoreSrcDB(string src_folder)
        {
            string param = "--drop " + DBHost + src_folder;
            EnqueueCammand(MongoDBRestoreToolPath, param);
        }

        private void RestoreTemprorilyBackedupDB()
        {
            string param = DBHost + TempFolder;
            EnqueueCammand(MongoDBRestoreToolPath, param);
        }

        private void CreateRestoreCommands4CurrentDB(string src_folder, DBPriority4Restore db_priority)
        {
            bool is_drop = db_priority == DBPriority4Restore.NoPriority;
            string drop_param = is_drop ? "--drop " : "";
            string param = drop_param + DBHost + src_folder;
            EnqueueCammand(MongoDBRestoreToolPath, param);
        }

        private string MongoDBRestoreToolPath
        {
            get
            {
                if (ExistOnStartupFolder(MongoRestoreTool))
                {
                    return System.IO.Path.Combine(StartupFolder(), MongoRestoreTool);
                }

                string tool_path = Util.ConfigReader.GetValue("DB_BACKUP_TOOL_PATH", "\\");
                if (ExistOnFolder(tool_path, MongoRestoreTool))
                {
                    return System.IO.Path.Combine(tool_path, MongoRestoreTool);
                }

                return MongoRestoreTool;
            }
        }
    }
}
