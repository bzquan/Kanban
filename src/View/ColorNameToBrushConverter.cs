using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Kanban
{
    public class ColorNameToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            DataGridCell dgc = value as DataGridCell;
            if (dgc == null) return Brushes.White;

            try
            {
                ViewModel.ProcessStep processStep = dgc.DataContext as ViewModel.ProcessStep;
                if (processStep != null)
                    return new BrushConverter().ConvertFromString(processStep.LabelColor);

                return DefaultBackground(dgc);
            }
            catch
            {
                return DefaultBackground(dgc);
            }
        }

        private Brush DefaultBackground(DataGridCell dgc) => dgc.IsSelected ? Brushes.DodgerBlue : Brushes.White;

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
