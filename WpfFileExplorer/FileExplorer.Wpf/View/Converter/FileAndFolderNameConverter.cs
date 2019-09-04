using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Data;

namespace FileExplorer.Wpf.View.Converter
{
    [ValueConversion(typeof(FileSystemTreeElement), typeof(string))]
    public class FileAndFolderNameConverter : IValueConverter
    {
      private static List<DriveInfo> DriveInfos { get; set; } 

      /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
      {
        if (!FileAndFolderNameConverter.DriveInfos?.Any() ?? true)
        {
          FileAndFolderNameConverter.DriveInfos = DriveInfo.GetDrives().ToList();
        }

        if (value is FileSystemTreeElement fileSystemTreeElement)
        {
          if (fileSystemTreeElement.IsSystemDirectory)
          {
            var currentDriveInfo = FileAndFolderNameConverter.DriveInfos.FirstOrDefault((driveInfo) =>
              driveInfo.RootDirectory.FullName.Equals(fileSystemTreeElement.ElementInfo.FullName,
                StringComparison.OrdinalIgnoreCase));

            return string.IsNullOrWhiteSpace(currentDriveInfo.VolumeLabel) 
              ? currentDriveInfo.RootDirectory.Name 
              : $"{fileSystemTreeElement.ElementInfo.Name} ({FileAndFolderNameConverter.DriveInfos.FirstOrDefault((driveInfo) => driveInfo.RootDirectory.FullName.Equals(fileSystemTreeElement.ElementInfo.FullName, StringComparison.OrdinalIgnoreCase)).VolumeLabel})";
          }

          return string.IsNullOrWhiteSpace(fileSystemTreeElement.AlternativeIElementName)
            ? fileSystemTreeElement.ElementInfo.Name
            : fileSystemTreeElement.AlternativeIElementName;
        }

        return Binding.DoNothing;
      }

      /// <inheritdoc />
      public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
