using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanban.ViewModel
{
    public delegate Task<Card> CardFactory(Board board, Model.WorkState cardWorkState);
    public delegate ProcessStepViewModel ProcessStepViewModelFactory(Board board, Repository.ProcessStep processStep);
    public delegate WIPDoneViewModel WIPDoneViewModelFactory(Board board, Model.WorkState cardWorkState, string cardFilter, bool loadCardsOnBackBoard);
    public delegate Model.IDBBackup DBBackupFactory(Model.IProcessExecutorClient processExecutorClient);
}
