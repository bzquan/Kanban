using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kanban
{
    public class ScrollingListBox : ListBox
    {
        public ScrollingListBox()
        {
            Util.EventAggregator<ViewModel.NewCardAddedArg>.Instance.Event += OnNewCardAddedArg;
        }

        private void OnNewCardAddedArg(object sender, ViewModel.NewCardAddedArg arg)
        {
            ScrollToBottomOfList();
        }

        public void ScrollToBottomOfList()
        {
            var border = (Border)VisualTreeHelper.GetChild(this, 0);
            var scrollViewer = (ScrollViewer)VisualTreeHelper.GetChild(border, 0);
            scrollViewer.ScrollToBottom();
        }
    }
}
