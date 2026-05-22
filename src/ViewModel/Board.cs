using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

using MongoDB.Bson;
using MongoDB.Driver;
using Kanban.Util;

namespace Kanban.ViewModel
{
    public delegate void CycleTimeDurationChangedHandler();

    public class Board : NotifyPropertyChangedBase
    {

        public ICommand GotoBoardPageCommand => new DelegateCommandNoArg(OnGotoBoardPage);
        public ICommand DeleteBoardCommand => new DelegateCommandNoArg(OnDeleteBoard);

        public Board(Model.Board board)
        {
            BoardModel = board;
        }

        private void OnGotoBoardPage()
        {
            GotoBoardPageRequestedArgs arg = new GotoBoardPageRequestedArgs(this);
            EventAggregator<GotoBoardPageRequestedArgs>.Instance.Publish(this, arg);
        }

        private void OnDeleteBoard()
        {
            DeleteBoardRequestedArgs arg = new DeleteBoardRequestedArgs(this);
            EventAggregator<DeleteBoardRequestedArgs>.Instance.Publish(this, arg);
        }

        public Model.Board BoardModel { get; set; }

        public ObjectId _id
        {
            get { return BoardModel._id; }
        }

        public int SeqNo
        {
            get { return BoardModel.SeqNo; }
            set { SetProperty(BoardModel, value); }
        }

        public string Title
        {
            get { return BoardModel.Title; }
            set { SetProperty(BoardModel, value); }
        }

        public string Description
        {
            get { return BoardModel.Description; }
            set { SetProperty(BoardModel, value); }
        }

        public Model.DevProcess DevProcess
        {
            get { return BoardModel.DevProcess; }
        }

        public List<Repository.ProcessStep> ProcessSteps
        {
            get { return DevProcess.ProcessSteps; }
        }

        public UpdateResult SaveProcessStep()
        {
            return BoardModel.SaveProcessStep();
        }

        public bool IsCycleTimeStepsStable()
        {
            return BoardModel.DevProcess.IsCycleTimeStepsStable();
        }

        public bool AdjustProcessCycleTimeSteps()
        {
            return BoardModel.AdjustProcessCycleTimeSteps();
        }

        public void SortProcessSteps()
        {
            DevProcess.SortProcessSteps();
        }

        public ProcessStep AddProcessStep()
        {
            Repository.ProcessStep processStepInfo = BoardModel.AddProcessStep();
            return new ProcessStep(processStepInfo);
        }

        public bool ShowDetailedCards { get; set; } = true;

        public UpdateResult ChangeCardsAndActivitySeqNo(int fromSeqNo, int toSeqNo)
        {
            return BoardModel.ChangeCardsAndActivitySeqNo(fromSeqNo, toSeqNo);
        }
    }
}
