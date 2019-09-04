using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace FileExplorer.Wpf.View.Converter
{
  /// <summary>
  /// Checks if value is <see cref="FileSystemTreeElement"/>. If parameter is set to true (default is false) then the root directory although its type matches, results to false.
  /// </summary>
  public class ItemIsDirectoryValueConverter : IValueConverter, IMultiValueConverter
  {
    #region Implementation of IValueConverter

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      if (parameter is bool ignoreRootDirectory)
      {

        return value is FileSystemTreeElement node && !(ignoreRootDirectory && node.ParentFileSystemTreeElement == null);
      }

      return Binding.DoNothing;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
      throw new NotSupportedException();
    }

    #endregion

    #region Implementation of IMultiValueConverter

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
      var allAreDirectories = false;
      values.ToList().ForEach((value) =>
      {
        allAreDirectories |= (bool) Convert(value, targetType, parameter, culture);
      });
      return allAreDirectories;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
      return new []{ConvertBack(value, targetTypes.FirstOrDefault(), parameter, culture)};
    }

    #endregion
  }
}
