namespace Kanban.ViewModel
{
    public delegate Task<Card> CardFactory(Board board, Model.WorkState cardWorkState, Card srcCard);
    public delegate ProcessStepViewModel ProcessStepViewModelFactory(Board board, Repository.ProcessStep processStep);
    public delegate WIPDoneViewModel WIPDoneViewModelFactory(Board board, Model.WorkState cardWorkState, string cardFilter, bool loadCardsOnBackBoard);
    public delegate Model.IDBBackup DBBackupFactory(Model.IProcessExecutorClient processExecutorClient);
}
