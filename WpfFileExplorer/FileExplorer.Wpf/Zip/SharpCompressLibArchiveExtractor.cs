using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace FileExplorer.Wpf.Zip
{
  public class SharpCompressLibArchiveExtractor : FileExtractor
  {
    #region Overrides of FileExtractor

    protected override bool TryExtractToFolder(FileInfo compressedFileInfo, DirectoryInfo destinationPathInfo)
    {
      if (!destinationPathInfo.Exists)
      {
        try
        {
          destinationPathInfo.Create();
        }
        catch (IOException)
        {
          return false;
        }

        // Update diredctory info, to update the 'Exists' property
        destinationPathInfo = new DirectoryInfo(destinationPathInfo.FullName);
      }

      this.lastProgressArgs = null;
      this.CurrentExtractingArchiveInfo = compressedFileInfo;
      this.ProgressExtractedBytes = 0;
      this.ProgressPercentage = 0;
      this.ExtractionStopwatch = new Stopwatch();
      this.ExtractionStopwatch.Start();

      using (IArchive archive = ArchiveFactory.Open(this.CurrentExtractingArchiveInfo,
        new ReaderOptions() {LeaveStreamOpen = false}))
      {
        this.UncompressedArchiveSizeInBytes =
          archive.Entries.Where((entry) => !entry.IsDirectory).Sum((entry) => entry.Size);
      }

      // This following line is expected to throw first chance exceptions, when the file format is not supported by the extraction library. Since there exists no API to test if a file type is supported (or an archive at all), this is the only extensible solution (in case that supported file type list was extended by the library)
      try
      {
        using (Stream stream = File.OpenRead(this.CurrentExtractingArchiveInfo.FullName))
        {
          this.UncompressedArchiveSizeInBytes = stream.Length;
          using (IReader archiveReader = ReaderFactory.Open(stream, new ReaderOptions()))
          {
            archiveReader.EntryExtractionProgress += SetEntryExtractionProgressOnReaderExtracting;

            try
            {
              while (archiveReader.MoveToNextEntry())
              {
                if (!archiveReader.Entry.IsDirectory)
                {
                  archiveReader.WriteEntryToDirectory(
                    destinationPathInfo.FullName,
                    new ExtractionOptions()
                    {
                      ExtractFullPath = true,
                      Overwrite = true,
                      PreserveAttributes = false,
                      PreserveFileTime = false
                    });
                }
              }
            }
            finally
            {
              archiveReader.EntryExtractionProgress -= SetEntryExtractionProgressOnReaderExtracting;
            }
          }
        }
      }
      catch (InvalidFormatException)
      {
        return false;
      }
      catch (FormatException)
      {
        return false;
      }
      catch (InvalidOperationException)
      {
        return false;
      }
      catch (NotSupportedException)
      {
        return false;
      }

      return true;
    }

    private void SetEntryExtractionProgressOnReaderExtracting(object sender, ReaderExtractionEventArgs<IEntry> e)
    {
      this.lastProgressArgs = e;

      ExtractionProgressEventArgs progressReport = CreateProgressReport();
      this.CurrentProgressReporter.Report(progressReport);
    }

    protected override ExtractionProgressEventArgs CreateProgressReport()
    {
      if (this.UncompressedArchiveSizeInBytes <= 0)
      {
        return new ExtractionProgressEventArgs(this.CurrentExtractingArchiveInfo,
          0, 0, 0,
          0, 0, "00:00:00");
      }

      if (this.lastProgressArgs != null)
      {
        this.ProgressExtractedBytes += this.lastProgressArgs.Item.CompressedSize;
        this.ProgressPercentage = Math.Min(this.ProgressExtractedBytes / (double) this.UncompressedArchiveSizeInBytes * 100d, 100d);
      }

      this.ElapsedTime = this.ExtractionStopwatch.Elapsed;
      this.TimeElapsedFormatted = this.ExtractionStopwatch.Elapsed.ToString(@"hh\:mm\:ss");
      
       return new ExtractionProgressEventArgs(this.CurrentExtractingArchiveInfo,
         this.ProgressExtractedBytes, this.lastProgressArgs?.ReaderProgress.Iterations ?? 0, (int) Math.Floor(this.ProgressPercentage),
         this.ProgressPercentage, this.UncompressedArchiveSizeInBytes, this.TimeElapsedFormatted);
    }

    protected ExtractionProgressEventArgs CreateElapsedTimeReport()
    {
      if (this.UncompressedArchiveSizeInBytes <= 0)
      {
        return new ExtractionProgressEventArgs(this.CurrentExtractingArchiveInfo,
          0, 0, 0,
          0, 0, "00:00:00");
      }

      this.ElapsedTime = this.ExtractionStopwatch.Elapsed;
      this.TimeElapsedFormatted = this.ExtractionStopwatch.Elapsed.ToString(@"hh\:mm\:ss");
      
       return new ExtractionProgressEventArgs(this.CurrentExtractingArchiveInfo,
         this.ProgressExtractedBytes, this.lastProgressArgs.ReaderProgress.Iterations, (int) this.ProgressPercentage,
         this.ProgressPercentage, this.UncompressedArchiveSizeInBytes, this.TimeElapsedFormatted);
    }
        #endregion

    private IEntry currentEntry;
    private ReaderExtractionEventArgs<IEntry> lastProgressArgs;

  }
}