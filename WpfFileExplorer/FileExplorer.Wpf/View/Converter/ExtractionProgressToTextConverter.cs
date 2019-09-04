using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Data;
using FileExplorer.Wpf.Zip;

namespace FileExplorer.Wpf.View.Converter
{
  public class ExtractionProgressToTextConverter : IMultiValueConverter
  {
    #region Implementation of IMultiValueConverter

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
      if (values.OfType<ObservableDictionary<FileSystemInfo, ExtractionProgressEventArgs>>().FirstOrDefault() is ObservableDictionary<FileSystemInfo, ExtractionProgressEventArgs> progressTable && values.OfType<FileSystemTreeElement>().FirstOrDefault() is FileSystemTreeElement currentFileSystemElement)
      {
        if (!FileExplorer.Instance.ExplorerViewModel.ArchiveExtractorInstanceTable.ContainsKey(currentFileSystemElement))
        {
          return string.Empty;
        }

        if (progressTable.TryGetValue(currentFileSystemElement.ElementInfo, out ExtractionProgressEventArgs progressEventArgs))
        {
          string readableUncompressedSize = progressEventArgs.UncompressedSizeInBytes.ToString();
          string unit = "B";
          if (progressEventArgs.UncompressedSizeInGigaByte > 1)
          {
            readableUncompressedSize = progressEventArgs.UncompressedSizeInGigaByte.ToString("F3");
            unit = "GB";
          }
          else if (progressEventArgs.UncompressedSizeInMegaByte > 1)
          {
            readableUncompressedSize = progressEventArgs.UncompressedSizeInMegaByte.ToString("F3");
            unit = "MB";
          }
          else if (progressEventArgs.UncompressedSizeInKiloByte > 1)
          {
            readableUncompressedSize = progressEventArgs.UncompressedSizeInKiloByte.ToString("F3");
            unit = "KB";
          }

          return $"Extracting file... ({progressEventArgs.TimeElapsedFormatted})    {progressEventArgs.PercentageRead:D}%  of  {readableUncompressedSize} {unit}";
        }
      }
      return string.Empty;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
      throw new NotSupportedException();
    }

    #endregion
  }
}
