using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

using Kanban.ViewModel;
using static Kanban.Util.EnumUtil;

namespace Kanban
{
    /// <summary>
    /// Interaction logic for EditCardDialog.xaml
    /// </summary>
    public partial class EditCardDialog : Window
    {
        private EditCardDialogViewModel ViewModel { get; set; }
        private IViewModelProperties Properties { get; }
        private Model.IActivityRepository ActivityRepository { get; }
        private Card Card { get; }

        public EditCardDialog(Board board, Card card, IViewModelProperties properties, Model.IActivityRepository activityRepository)
        {
            InitializeComponent();
            Properties = properties;
            ActivityRepository = activityRepository;
            Card = card;

            InitializeViewModel(board, card);
        }

        private void InitializeViewModel(Board board, Card card)
        {
            InitComboBoxByEnum<Repository.CardType>(CardTypeComboBox);

            ViewModel = new EditCardDialogViewModel(board, card, Properties, ActivityRepository);
            activityDataGrid.ItemsSource = ViewModel.ActivityTable.DefaultView;
            this.DataContext = ViewModel;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RequestDisallowDragAndDropCards();
        }

        private void RequestDisallowDragAndDropCards()
        {
            Util.EventAggregator<DragAndDropLib.DisallowDragAndDropRequestArg>.Instance.Publish(this, new DragAndDropLib.DisallowDragAndDropRequestArg());
        }

        private void OnClosed(object sender, EventArgs e)
        {
            RequestAllowDragAndDropCards();
            Card.RefreshCardContentsInfo();
        }

        private void RequestAllowDragAndDropCards()
        {
            Util.EventAggregator<DragAndDropLib.AllowDragAndDropRequestArg>.Instance.Publish(this, new DragAndDropLib.AllowDragAndDropRequestArg());
        }
    }
}
