using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BionicLibrary.NetStandard.Generic;
using BionicLibrary.NetStandard.IO;
using JetBrains.Annotations;
using SharpCompress.Common;

namespace FileExplorer.Wpf.Zip
{
  /// <summary>
  /// Opens and extract archives using SharpCompress library [https://github.com/adamhathcock/sharpcompress].<para/>
  /// Supported archive types: Zip, GZip, 7Zip, Rar, Tar.
  /// </summary>
  public abstract class FileExtractor : INotifyPropertyChanged, IFileExtractor, IDisposable
  {
    static FileExtractor()
    {
      FileExtractor.ExtractedFileInfosHistory = new Dictionary<string, DirectoryInfo>();
    }

    protected FileExtractor()
    {
    }

    /// <summary>
    /// Extract ZIP file to temp folder and open the destination folder
    /// </summary>
    /// <param name="compressedFileInfo"><see cref="FileInfo"/> of the .zip source</param>
    /// <param name="progressReporter"></param>
    /// <returns><code>True</code> on success.</returns>
    public async Task<(bool IsSuccessful, DirectoryInfo DestinationDirectory)> ExtractZipArchiveAsync(FileInfo compressedFileInfo, IProgress<ExtractionProgressEventArgs> progressReporter)
    {
      this.CurrentProgressReporter = progressReporter;
      if (!compressedFileInfo.Exists)
      {
        return (false, new DirectoryInfo(compressedFileInfo.FullName));
      }


      this.IsPasswordRequired = false;

      // Name temporary extraction folder after file (including extension)
      string extractionFolderPathRoot = Path.Combine(System.IO.Path.GetTempPath(), compressedFileInfo.Name);
      if (FileExtractor.ExtractedFileInfosHistory.TryGetValue(compressedFileInfo.FullName,
            out DirectoryInfo extractedHistoryPath) && Directory.Exists(extractedHistoryPath.FullName))
      {
        return (true, extractedHistoryPath);
      }
        

      var i = 0;
      string uniqueExtractionFolderPath = extractionFolderPathRoot;
      while (Directory.Exists(uniqueExtractionFolderPath))
      {
        uniqueExtractionFolderPath = extractionFolderPathRoot + "." + i++.ToString("000");
      }

      var destinationPathInfo = new DirectoryInfo(uniqueExtractionFolderPath);

      // Store destination paths for later clean up
      FileExtractor.ExtractedFileInfosHistory?.Add(compressedFileInfo.FullName, destinationPathInfo);

      (bool IsSuccessful, DirectoryInfo DestinationDirectory) result = (false, destinationPathInfo);
      try
      {
        await Task.Run(() => result.IsSuccessful = TryExtractToFolder(compressedFileInfo, destinationPathInfo)).ConfigureAwait(false);
      }
      catch (InvalidFormatException)
      {
        result.IsSuccessful = false;
        return result;
      }
      catch (FormatException)
      {
        result.IsSuccessful = false;
        return result;
      }
      catch (InvalidOperationException)
      {
        result.IsSuccessful = false;
        return result;
      }
      catch (NotSupportedException)
      {
        result.IsSuccessful = false;
        return result;
      }

      OnExtractionCompleted(result);
      return result;
    }

    public static bool FileIsArchive(FileInfo fileInfo)
    {
      var fileExtensionWhithoutDot = string.Empty;
      return !string.IsNullOrWhiteSpace(fileInfo.Extension) 
             &&  (fileExtensionWhithoutDot = fileInfo.Extension.Substring(1))
               .Equals(FileExtensions.Zip.ToString("G"), StringComparison.OrdinalIgnoreCase)
             || fileExtensionWhithoutDot.Equals(FileExtensions.Rar.ToString("G"), StringComparison.OrdinalIgnoreCase)
             || fileExtensionWhithoutDot.Equals(FileExtensions.Gz.ToString("G"), StringComparison.OrdinalIgnoreCase)
             || fileExtensionWhithoutDot.Equals(FileExtensions.Tar.ToString("G"), StringComparison.OrdinalIgnoreCase)
             || fileExtensionWhithoutDot.Equals(FileExtensions.Bz2.ToString("G"), StringComparison.OrdinalIgnoreCase)
             || fileExtensionWhithoutDot.Equals(FileExtensions.SevenZip.ToString("G"), StringComparison.OrdinalIgnoreCase);
    }

    // Uses SharpCompress library [https://github.com/adamhathcock/sharpcompress]
    protected abstract bool TryExtractToFolder(FileInfo compressedFileInfo, DirectoryInfo destinationPathInfo);
 
    public void CleanUpExtractedFiles()
    {
      FileExtractor.CleanUpExtractedTemporaryFiles();
    }
 
    public static void CleanUpExtractedTemporaryFiles()
    {
      if (FileExtractor.ExtractedFileInfosHistory.Any())
      {
        try
        {
          FileExtractor.ExtractedFileInfosHistory
            .Where((directoryInfoEntry) => Directory.Exists(directoryInfoEntry.Value.FullName))
            .ToList().ForEach((directoryInfoEntry) => directoryInfoEntry.Value.Delete(true));
        }
        catch (Exception)
        {}

        FileExtractor.ExtractedFileInfosHistory.Clear();
      }
    }

    protected virtual void OnExtractionCompleted((bool IsSuccessful, DirectoryInfo DestinationDirectory) e)
    {
      this.ExtractionCompleted?.Invoke(this, new ValueChangedEventArgs<(bool IsSuccessful, DirectoryInfo DestinationDirectory)>(e, e));
    }

    protected virtual void OnPasswordRequired()
    {
      this.IsPasswordRequired = true;
      this.PasswordRequired?.Invoke(this, EventArgs.Empty);
    }

    [NotifyPropertyChangedInvocator]
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected abstract ExtractionProgressEventArgs CreateProgressReport();
    

    public static Dictionary<string, DirectoryInfo> ExtractedFileInfosHistory { get; protected set; }

    public event EventHandler<ExtractionProgressEventArgs> ProgressChanged;
    public event PropertyChangedEventHandler PropertyChanged;
    public event EventHandler<ValueChangedEventArgs<(bool IsSuccessful, DirectoryInfo DestinationDirectory)>> ExtractionCompleted;
    public event EventHandler PasswordRequired;

    private TimeSpan elapsedTime;
    public TimeSpan ElapsedTime
    {
      get => this.elapsedTime;
      protected set
      {
        if (value.Equals(this.elapsedTime))
        {
          return;
        }
        this.elapsedTime = value;
        OnPropertyChanged();
      }
    }

    private bool isPasswordRequired;
    public bool IsPasswordRequired
    {
      get => this.isPasswordRequired;
      private set
      {
        if (value == this.isPasswordRequired)
        {
          return;
        }
        this.isPasswordRequired = value;
        OnPropertyChanged();
      }
    }

    private long progressExtractedBytes;
    public long ProgressExtractedBytes
    {
      get => this.progressExtractedBytes;
      protected set
      {
        if (value == this.progressExtractedBytes)
        {
          return;
        }
        this.progressExtractedBytes = value;
        OnPropertyChanged();
      }
    }

    private double progressPercentage;
    public double ProgressPercentage
    {
      get => this.progressPercentage;
      protected set
      {
        if (value.Equals(this.progressPercentage))
        {
          return;
        }
        this.progressPercentage = value;
        OnPropertyChanged();
      }
    }

    private string timeElapsedFormatted;

    public string TimeElapsedFormatted
    {
      get => this.timeElapsedFormatted;
      set
      {
        if (value == this.timeElapsedFormatted)
        {
          return;
        }
        this.timeElapsedFormatted = value;
        OnPropertyChanged();
      }
    }

    private IProgress<ExtractionProgressEventArgs> currentProgressReporter;   
    public IProgress<ExtractionProgressEventArgs> CurrentProgressReporter
    {
      get { return this.currentProgressReporter; }
      protected set 
      { 
        this.currentProgressReporter = value; 
        OnPropertyChanged();
      }
    }

    public long UncompressedArchiveSizeInBytes { get; protected set; }
    public FileInfo CurrentExtractingArchiveInfo { get; protected set; }
    protected Stopwatch ExtractionStopwatch { get; set; }

    #region Implementation of IDisposable

    public void Dispose()
    {
      this.Dispose(true);

      // Prevent trying to dispose 'this' more than once by removing 
      // 'this' reference from the GC finalizer queue
      GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool invokedByClient)
    {
      if (!this.IsDisposed)
      {
        if (invokedByClient)
        {
          
        }

        if (FileExtractor.ExtractedFileInfosHistory?.Any() ?? false)
        {
          CleanUpExtractedFiles();
          FileExtractor.ExtractedFileInfosHistory = null;
        }
      }
      this.IsDisposed = true;
    }

    private bool IsDisposed { get; set; }

    #endregion

    
  }
}
