using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using FileExplorer.Wpf.Zip;

namespace FileExplorer.Wpf.ViewModel
{
  public interface IFileExplorerViewModel : INotifyPropertyChanged
  {
    void ClearFileSystemTree();
    void SetIsExtracting();
    void ClearIsExtracting();
    void SetIsBusy();
    void ClearIsBusy();
    Task AddFilesAsync(List<string> newFilePaths, bool isRootFolderExpanded);
    Task<(bool IsSuccessful, DirectoryInfo DestinationDirectory)> ExtractArchive(FileSystemTreeElement archiveFileTreeElement);
    ICommand RemoveFileCommand { get; }
    ICommand FilterFilesByCustomExtensionsCommand { get; }
    ICommand FilterFilesByDefaultExtensionsCommand { get; }
    bool IsReplacingFilesOnAddNew { get; set; }
    bool IsBusy { get; }
    bool IsDeleteExtractedFilesOnExitEnabled { get; set; }
    bool HasExtractionsRunning { get; }
    bool IsExplorerInDefaultState { get; }
    ObservableCollection<string> RawFilePaths { get; set; }
    FileSystemTreeElement VirtualExplorerRootDirectory { get; }
    ObservableDictionary<FileSystemTreeElement, IFileExtractor> ArchiveExtractorInstanceTable { get; }
    ObservableDictionary<FileSystemInfo, ExtractionProgressEventArgs> ExtractionProgressTable { get; }
    string FileFilterExtensions { get; set; }
    bool IsFilteringFileSystemTreeByCustomExtensions { get; set; }
    bool IsShowingLogFiles { get; set; }
    bool IsShowingTxtFiles { get; set; }
    bool IsShowingAnyFiles { get; set; }
    bool IsShowingIniFiles { get; set; }
    bool IsShowingArchiveFiles { get; set; }
    bool IsExtensionFilterEnabled { get; set; }
  }
}