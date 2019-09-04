using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Data;

namespace FileExplorer.Wpf.View.Converter
{
  [ValueConversion(typeof(FileInfo), typeof(bool))]
    class FileIsExtractingConverter : IValueConverter, IMultiValueConverter
    {
      /// <inheritdoc />
      public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
      {
        if (value is FileSystemTreeElement fileSystemTreeElement)
        {
          return FileExplorer.Instance.ExplorerViewModel.ArchiveExtractorInstanceTable.ContainsKey(fileSystemTreeElement);
        }

        return false;
      }

      /// <inheritdoc />
      public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
      {
        throw new NotSupportedException();
      }

      /// <inheritdoc />
      public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
      {
        if (values.OfType<FileSystemTreeElement>().FirstOrDefault() is FileSystemTreeElement fileSystemElementToCheck 
            && values.OfType<bool>().FirstOrDefault() is bool isExtractingFiles)
        {
          bool tryGetValue = FileExplorer.Instance.ExplorerViewModel?.ArchiveExtractorInstanceTable?.ContainsKey(fileSystemElementToCheck) ?? false;

          return tryGetValue;
        }

        return false;
      }

      /// <inheritdoc />
      public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
