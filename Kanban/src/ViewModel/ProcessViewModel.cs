using MongoDB.Driver;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using static Kanban.Util.KanbanDefinitions;
using static Kanban.Util.StringExtension;

namespace Kanban.ViewModel
{
    public class ProcessViewModel : NotifyPropertyChangedBase
    {
        const int WORK_SEQ_NO = 100;

        private INotifyPropertyChanged m_ParentViewModel;
        private Board m_Board;
        private Model.DevProcess m_DevProcess;
        private ObservableCollection<ProcessStep> m_ProcessSteps = new ObservableCollection<ProcessStep>();
        private IViewModelProperties m_Properties;
        private Model.IBoardRepository m_BoardRepository;
        private Model.ICardRepository m_CardRepository;

        public DelegateCommandNoArg AddProcessStepCommand => new DelegateCommandNoArg(OnAddProcessStep, CanAddProcessStep);
        public ICommand DeleteProcessStepCommand => new DelegateCommand<ProcessStep>(OnDeleteProcessStep);

        public event CycleTimeDurationChangedHandler CycleTimeDurationChanged;

        public ProcessViewModel(INotifyPropertyChanged parentViewModel, IViewModelProperties properties, Model.IBoardRepository boardRepository, Model.ICardRepository cardRepository)
        {
            m_ParentViewModel = parentViewModel;
            m_Properties = properties;
            m_BoardRepository = boardRepository;
            m_CardRepository = cardRepository;

            m_ProcessSteps.CollectionChanged += OnProcessStepCollectionChanged;
        }

        public Board Board
        {
            get { return m_Board; }
            set
            {
                m_Board = value;

                UnRegisterEventHandlerToProcessStepCollection();
                m_ProcessSteps.Clear();

                if (m_Board != null)
                {
                    m_DevProcess = m_Board.DevProcess;
                    foreach (var step in m_DevProcess.ProcessSteps)
                    {
                        m_ProcessSteps.Add(new ProcessStep(step));
                    }
                }
                RegisterEventHandlerToProcessStepCollection();
            }
        }

        public ObservableCollection<ProcessStep> ProcessSteps
        {
            get { return m_ProcessSteps; }
        }

        private void OnAddProcessStep()
        {
            UnRegisterEventHandlerToProcessStepCollection();

            ProcessStep newProcessStep = Board.AddProcessStep();
            m_ProcessSteps.Add(newProcessStep);

            RegisterEventHandlerToProcessStepCollection();
        }

        private bool CanAddProcessStep() => (m_Board != null);

        private void OnDeleteProcessStep(ProcessStep step)
        {
            if (!CanDeleteProcessStep(step))
            {
                string msg = String.Format(m_Properties.Message_DeleteProcessStepError, step.Name.Truncate(30));
                MessageBox.Show(msg,
                                m_Properties.Message_DeleteErrorTitle,
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
            else
            {
                string msg = String.Format(m_Properties.Message_DeleteProcessStepComfirm, step.Name.Truncate(30));
                MessageBoxResult result = MessageBox.Show(msg,
                                                          m_Properties.Message_DeleteComfirmTitle,
                                                          MessageBoxButton.YesNo,
                                                          MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    DeleteProcessStep(step);
                }
            }
        }

        private bool CanDeleteProcessStep(ProcessStep step)
        {
            if (m_ProcessSteps.Count <= 3) return false;

            if (m_CardRepository.ExistCards(Board.BoardModel, step.ProcessStepInfo))
                return false;
            else
                return true;
        }

        private bool DeletingProcessStep { get; set; } = false;
        private async void DeleteProcessStep(ProcessStep step)
        {
            DeletingProcessStep = true;
            UnRegisterEventHandlerToProcessStepCollection();

            await Board.BoardModel.DeleteProcessStep(step.ProcessStepInfo);
            m_ProcessSteps.Remove(step);
            AdjustProcessCycleTimeSteps();

            RegisterEventHandlerToProcessStepCollection();
            DeletingProcessStep = false;
        }

        private bool ProcessingOnProcessStepCollectionChanged { get; set; } = false;
        private void OnProcessStepCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (DeletingProcessStep) return;

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    // Addは別途OnAddProcessStepで処理したので、本処理は移動中のAddである。
                    // 移動中のAdd後、Removeイベントが発行されるので、Remove動作でProcessStepの
                    // 変更処理を行う。
                    break;
                case NotifyCollectionChangedAction.Remove:
                    if (ProcessingOnProcessStepCollectionChanged) return;

                    ProcessingOnProcessStepCollectionChanged = true;
                    ReorderProcessSteps();
                    AdjustProcessCycleTimeSteps();
                    ProcessingOnProcessStepCollectionChanged = false;
                    break;
                case NotifyCollectionChangedAction.Move:
                case NotifyCollectionChangedAction.Replace:
                case NotifyCollectionChangedAction.Reset:
                    // なにもしない
                    break;
            }
        }

        private void OnProcessStepPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProcessStep.IsStartOfCycleTime) ||
                e.PropertyName == nameof(ProcessStep.IsEndOfCycleTime))
            {
                AdjustProcessCycleTimeSteps();
            }
            else
            {
                Board.SaveProcessStep();
            }
        }

        enum ProcessStepsReorderKind { NoChange, Swap, Reorder }
        class SeqNoPair
        {
            public int NewSeqNo { get; set; }
            public int OldSeqNo { get; set; }
        }

        private UpdateResult ReorderProcessSteps()
        {
            List<SeqNoPair> changedSeqNoList = GetChangedProcessStepSeqNo();

            switch (CheckReorderKind(changedSeqNoList.Count))
            {
                case ProcessStepsReorderKind.Swap:
                    Board.ChangeCardsAndActivitySeqNo(changedSeqNoList[0].OldSeqNo, WORK_SEQ_NO);
                    Board.ChangeCardsAndActivitySeqNo(changedSeqNoList[1].OldSeqNo, changedSeqNoList[1].NewSeqNo);
                    return Board.ChangeCardsAndActivitySeqNo(WORK_SEQ_NO, changedSeqNoList[0].NewSeqNo);
                case ProcessStepsReorderKind.Reorder:
                    return ReorderProcessSteps(changedSeqNoList);
                default:
                    return UpdateResult.Unacknowledged.Instance;

            }
        }

        private List<SeqNoPair> GetChangedProcessStepSeqNo()
        {
            List<SeqNoPair> changedSeqNoList = new List<SeqNoPair>();
            int index = 0;
            foreach (var step in m_ProcessSteps)
            {
                if (step.PhaseSeqNo != index) changedSeqNoList.Add(new SeqNoPair { NewSeqNo = index, OldSeqNo = step.PhaseSeqNo });
                index++;
            }

            return changedSeqNoList;
        }

        private static ProcessStepsReorderKind CheckReorderKind(int updatedCount)
        {
            if (updatedCount == 0)
                return ProcessStepsReorderKind.NoChange;
            else if (updatedCount == 2)
                return ProcessStepsReorderKind.Swap;
            else
                return ProcessStepsReorderKind.Reorder;
        }

        private UpdateResult ReorderProcessSteps(List<SeqNoPair> changedSeqNoList)
        {
            SeqNoPair movedProcess = changedSeqNoList.FirstOrDefault(x => Math.Abs(x.NewSeqNo - x.OldSeqNo) > 1);
            if (movedProcess != null)
            {
                changedSeqNoList.Remove(movedProcess);
                Board.ChangeCardsAndActivitySeqNo(movedProcess.OldSeqNo, WORK_SEQ_NO);
            }

            changedSeqNoList.Sort((x, y) => x.OldSeqNo - y.OldSeqNo);
            SeqNoPair firstReordedSeqNo = changedSeqNoList.First();
            bool forward = firstReordedSeqNo.NewSeqNo > firstReordedSeqNo.OldSeqNo;
            UpdateResult result = m_BoardRepository.MoveCard(Board.BoardModel, firstReordedSeqNo.OldSeqNo, changedSeqNoList.Last().OldSeqNo, forward);

            if (movedProcess != null)
                result = Board.ChangeCardsAndActivitySeqNo(WORK_SEQ_NO, movedProcess.NewSeqNo);

            return result;
        }

        private bool AdjustProcessCycleTimeSteps()
        {
            if (!Board.IsCycleTimeStepsStable()) return false;

            UnRegisterEventHandlerToProcessStepCollection();

            AdjustProcessStepSeqNoAndLabelColor();
            bool hasAdjusted = Board.AdjustProcessCycleTimeSteps();
            if (hasAdjusted)
            {
                foreach (ProcessStep step in m_ProcessSteps)
                {
                    step.NotifyCycleTimeChanged();
                }
            }

            RegisterEventHandlerToProcessStepCollection();
            CycleTimeDurationChanged?.Invoke();

            return hasAdjusted;
        }

        private void AdjustProcessStepSeqNoAndLabelColor()
        {
            int index = 0;
            foreach (var processStep in m_ProcessSteps)
            {
                processStep.PhaseSeqNo = index;
                processStep.LabelColor = LabelColorNames[index % LabelColorNames.Count];
                index++;
            }
            Board.SortProcessSteps();
        }

        private void RegisterEventHandlerToProcessStepCollection()
        {
            m_ProcessSteps.CollectionChanged += OnProcessStepCollectionChanged;
            foreach (ProcessStep processStep in m_ProcessSteps)
            {
                processStep.PropertyChanged += OnProcessStepPropertyChanged;
            }
        }

        private void UnRegisterEventHandlerToProcessStepCollection()
        {
            m_ProcessSteps.CollectionChanged -= OnProcessStepCollectionChanged;
            foreach (ProcessStep processStep in m_ProcessSteps)
            {
                processStep.PropertyChanged -= OnProcessStepPropertyChanged;
            }
        }
    }
}
