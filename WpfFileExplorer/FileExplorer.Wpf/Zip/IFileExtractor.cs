using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using BionicLibrary.NetStandard.Generic;

namespace FileExplorer.Wpf.Zip
{
  public interface IFileExtractor : IDisposable
  {
    FileInfo CurrentExtractingArchiveInfo { get; }
    TimeSpan ElapsedTime { get; }
    bool IsPasswordRequired { get; }
    long ProgressExtractedBytes { get; }
    double ProgressPercentage { get; }
    long UncompressedArchiveSizeInBytes { get; }

    event EventHandler<ValueChangedEventArgs<(bool IsSuccessful, DirectoryInfo DestinationDirectory)>> ExtractionCompleted;
    event EventHandler PasswordRequired;
    event EventHandler<ExtractionProgressEventArgs> ProgressChanged;
    event PropertyChangedEventHandler PropertyChanged;

    void CleanUpExtractedFiles();
    Task<(bool IsSuccessful, DirectoryInfo DestinationDirectory)> ExtractZipArchiveAsync(
      FileInfo compressedFileInfo,
      IProgress<ExtractionProgressEventArgs> progressReporter);
  }
}