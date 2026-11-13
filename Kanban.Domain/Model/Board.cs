using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Kanban.Model
{
    public class Board : KanbanItem
    {
        public Repository.Board BoardOfRepository { get; set; }

        public DevProcess DevProcess { get; set; }

        public Board(string title, string description, DevProcess defaultDevProcess) : this(new Repository.Board { Title = title, Description = description })
        {
            DevProcess = defaultDevProcess;
            BoardOfRepository.ProcessSteps = defaultDevProcess.ProcessSteps;
        }

        public Board(Repository.Board board) : base(board)
        {
            BoardOfRepository = board;

            if (board.ProcessSteps != null)
            {
                DevProcess = new DevProcess(board.ProcessSteps);
            }
        }

        public string Description
        {
            get { return BoardOfRepository.Description; }
            set
            {
                BoardOfRepository.Description = value;
                UpdateProperty(value);
            }
        }

        public string Developers
        {
            get { return BoardOfRepository.Developers; }
            set
            {
                BoardOfRepository.Developers = value;
                UpdateProperty(value);
            }
        }

        private List<Repository.ProcessStep> ProcessSteps
        {
            get { return DevProcess.ProcessSteps; }
        }

        public Repository.ProcessStep AddProcessStep()
        {
            Repository.ProcessStep step = DevProcess.AddProcessStep();
            SaveProcessStep();
            return step;
        }

        public async Task<UpdateResult> DeleteProcessStep(Repository.ProcessStep step)
        {
            DevProcess.DeleteProcessStep(step);
            await BoardRepository.DeleteActivities(this, step.PhaseSeqNo);

            int fromSeqNo = step.PhaseSeqNo + 1;
            int toSeqNo = BoardOfRepository.ProcessSteps.Last().PhaseSeqNo;
            BoardRepository.MoveCard(this, fromSeqNo, toSeqNo, forward: false);

            return SaveProcessStep();
        }

        public UpdateResult ChangeCardsAndActivitySeqNo(int fromSeqNo, int toSeqNo)
        {
            return BoardRepository.ChangeCardsAndActivitySeqNo(this, fromSeqNo, toSeqNo);
        }

        public bool AdjustProcessCycleTimeSteps()
        {
            bool hasAdjusted = DevProcess.AdjustProcessCycleTimeSteps();
            SaveProcessStep();
            return hasAdjusted;
        }

        public UpdateResult SaveProcessStep()
        {
            return BoardRepository.Update(this, nameof(ProcessSteps), ProcessSteps);
        }

        public IEnumerable<Repository.ProcessStep> GetCycleTimeSteps()
        {
            return DevProcess.GetCycleTimeSteps();
        }

        protected override async void Update<T>(T propertyValue, [CallerMemberName] string propertyName = "")
        {
            await BoardRepository.UpdateAsync<T>(this, propertyName, propertyValue);
        }

        public void SetDefaultCycleTimeDuration()
        {
            if (DevProcess.SetDefaultCycleTimeDuration())
            {
                SaveProcessStep();
            }
        }

        public static IBoardRepository BoardRepository { get; set; }
    }
}
