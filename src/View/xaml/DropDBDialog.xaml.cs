using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Kanban.ViewModel;

namespace Kanban
{
    /// <summary>
    /// Interaction logic for DropDBDialog.xaml
    /// </summary>
    public partial class DropDBDialog : Window
    {
        DropDBDialogViewModel m_ViewModel;

        public DropDBDialog(DropDBDialogViewModel viewModel)
        {
            InitializeComponent();

            m_ViewModel = viewModel;
            m_ViewModel.LoadDatabaseName();
            this.DataContext = m_ViewModel;
        }

        private void OnCloseButtonClicked(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
