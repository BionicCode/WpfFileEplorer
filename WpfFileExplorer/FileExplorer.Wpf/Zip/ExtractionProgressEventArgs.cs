using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace FileExplorer.Wpf.Zip
{
  public class ExtractionProgressEventArgs : EventArgs, INotifyPropertyChanged
  {
    public ExtractionProgressEventArgs(
      FileInfo archiveInfo,
      long bytesTransferred,
      int iterations,
      int percentageRead,
      double percentageReadExact,
      long uncompressedSizeInBytes,
      string timeElapsedFormatted)
    {
      this.ArchiveInfo = archiveInfo;
      this.BytesTransferred = bytesTransferred;
      this.Iterations = iterations;
      this.PercentageRead = percentageRead;
      this.PercentageReadExact = percentageReadExact;
      this.UncompressedSizeInBytes = uncompressedSizeInBytes;
      this.TimeElapsedFormatted = timeElapsedFormatted;
    }

    public void Update(ExtractionProgressEventArgs progressArgs)
    {
      if (!progressArgs.ArchiveInfo.FullName.Equals(this.ArchiveInfo.FullName))
      {
        throw new ArgumentException(@"The progress argument maps to a different archive file.", nameof(progressArgs));
      }

      this.ArchiveInfo = progressArgs.ArchiveInfo;
      this.BytesTransferred = progressArgs.BytesTransferred;
      this.Iterations = progressArgs.Iterations;
      this.PercentageRead = progressArgs.PercentageRead;
      this.PercentageReadExact = progressArgs.PercentageReadExact;
      this.UncompressedSizeInBytes = progressArgs.UncompressedSizeInBytes;
      this.TimeElapsedFormatted = progressArgs.TimeElapsedFormatted;
    }

    private FileInfo archiveInfo;
    public FileInfo ArchiveInfo
    {
      get => this.archiveInfo;
      set
      {
        this.archiveInfo = value;
        OnPropertyChanged();
      }
    }

    private long bytesTransferred;
    public long BytesTransferred
    {
      get => this.bytesTransferred;
      set
      {
        this.bytesTransferred = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(this.MegaBytesTransferred));
      }
    }

    public double MegaBytesTransferred => ConvertBytesTo(this.BytesTransferred, 2);

    private long uncompressedSizeInBytes;
    public long UncompressedSizeInBytes
    {
      get => this.uncompressedSizeInBytes;
      set
      {
        this.uncompressedSizeInBytes = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(this.UncompressedSizeInKiloByte));
        OnPropertyChanged(nameof(this.UncompressedSizeInMegaByte));
        OnPropertyChanged(nameof(this.UncompressedSizeInGigaByte));
      }
    }

    private string timeElapsedFormatted;
    public string TimeElapsedFormatted
    {
      get => this.timeElapsedFormatted;
      set
      {
        this.timeElapsedFormatted = value;
        OnPropertyChanged();
      }
    }

    public double UncompressedSizeInKiloByte => ConvertBytesTo(this.UncompressedSizeInBytes, 1);
    public double UncompressedSizeInMegaByte => ConvertBytesTo(this.UncompressedSizeInBytes, 2);
    public double UncompressedSizeInGigaByte => ConvertBytesTo(this.UncompressedSizeInBytes, 3);

    private int iterations;
    public int Iterations
    {
      get => this.iterations;
      set
      {
        this.iterations = value;
        OnPropertyChanged();
      }
    }

    private int percentageRead;
    public int PercentageRead
    {
      get => this.percentageRead;
      set
      {
        this.percentageRead = value;
        OnPropertyChanged();
      }
    }

    private double percentageReadExact;
    public double PercentageReadExact
    {
      get => this.percentageReadExact;
      set
      {
        this.percentageReadExact = value;
        OnPropertyChanged();
      }
    }

    private double ConvertBytesTo(long bytes, int exponent)
    {
      return bytes / Math.Pow(1024, exponent);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    [NotifyPropertyChangedInvocator]
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
  }
}
