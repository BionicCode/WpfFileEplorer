using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Data;

namespace FileExplorer.Wpf.View.Converter
{
  [ValueConversion(typeof(ObservableCollection<FileSystemTreeElement>), typeof(ObservableCollection<FileSystemTreeElement>))]
  class FileSystemTreeElemetToFileInfosConverter : IValueConverter
  {
    #region Implementation of IValueConverter

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      if (value is ObservableCollection<FileSystemTreeElement> fileSystemElements)
      {
        return new ObservableCollection<FileSystemTreeElement>(
          fileSystemElements.Where((fileSystemTreeElement) => fileSystemTreeElement.ElementInfo is FileInfo));
      }

      return Binding.DoNothing;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
      throw new NotSupportedException();
    }

    #endregion
  }
}
