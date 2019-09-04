using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;

namespace FileExplorer.Wpf.View.Converter
{
    public class FileInfoToColorMultiValueConverter : IMultiValueConverter
    {
      #region Implementation of IMultiValueConverter

      public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
      {
        var fileInfo = values.OfType<FileInfo>().FirstOrDefault();
        var fileColorMap = values.OfType<ObservableDictionary<string, Color>>().FirstOrDefault();
        if (fileInfo == null || fileColorMap == null)
        {
          return Colors.Transparent;
        }
        
        return fileColorMap.TryGetValue(fileInfo.FullName, out Color fileInfoColor)
          ? fileInfoColor
          : Colors.Transparent;
      }

      public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
      {
        throw new NotSupportedException();
      }

      #endregion
    }
}
