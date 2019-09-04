using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using FileExplorer.Wpf.Zip;

namespace FileExplorer.Wpf.View.Converter
{
  public class ExtractionProgressConverter : IMultiValueConverter
  {
    #region Implementation of IMultiValueConverter

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
      if (values.OfType<FileSystemTreeElement>().FirstOrDefault() is FileSystemTreeElement currentFileSystemElement)
      { 
        if (FileExplorer.Instance.ExplorerViewModel.ExtractionProgressTable.TryGetValue(currentFileSystemElement.ElementInfo, out ExtractionProgressEventArgs progressEventArgs))
        {
          return (double) progressEventArgs.PercentageRead;
        }
      }
      return 0d;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
      throw new NotSupportedException();
    }

    #endregion
  }
}
