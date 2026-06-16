using System;
using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Kanban
{
    public class HighlighterConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            DataGridCell dataGridCell = values[0] as DataGridCell;
            try
            {
                ViewModel.ProcessStep processStep = dataGridCell.DataContext as ViewModel.ProcessStep;
                object brush = (processStep != null) ? new BrushConverter().ConvertFromString(processStep.LabelColor) : DefaultBackground(dataGridCell);
                return brush;

            }
            catch
            {
                return DefaultBackground(dataGridCell);
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }

        private Brush DefaultBackground(DataGridCell dataGridCell) =>
            dataGridCell.IsSelected ? Brushes.DodgerBlue : Brushes.White;
    }
}