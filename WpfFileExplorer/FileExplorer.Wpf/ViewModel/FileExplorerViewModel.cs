using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using BionicLibrary.Net.Utility;
using BionicLibrary.NetStandard.Generic;
using BionicLibrary.NetStandard.IO;
using FileExplorer.Wpf.Zip;
using JetBrains.Annotations;

namespace FileExplorer.Wpf.ViewModel
{
  public class FileExplorerViewModel : IFileExplorerViewModel
  {
    public FileExplorerViewModel()
    {
      // Create the virtual root for later binding of its child collection to a TreeViews's items source. 
      this.VirtualExplorerRootDirectory = FileSystemTreeElement.EmptyDirectoryNode;

      this.IsDeleteExtractedFilesOnExitEnabled = true;
      this.RawFilePaths = new ObservableCollection<string>();
      this.FileInfoIdColorMap = new ObservableDictionary<string, Color>();
      this.ArchiveExtractorInstanceTable = new ObservableDictionary<FileSystemTreeElement, IFileExtractor>();
      this.ExtractionProgressTable = new ObservableDictionary<FileSystemInfo, ExtractionProgressEventArgs>();
      
      Application.Current.LoadCompleted += (s, e) => InitializeRootFolder();
    }

    private async void InitializeRootFolder()
    {
      if (this.VirtualExplorerRootDirectory.ChildFileSystemTreeElements.Any())
      {
        return;
      }

      // Preload available system drives
      List<DriveInfo> driveInfos = DriveInfo.GetDrives()
        .Where((driveInfo) => !driveInfo.DriveType.HasFlag(DriveType.CDRom))
        .OrderBy((driveInfo) => driveInfo.Name).ToList();

      this.isInitializing = true;
      await AddFilesAsync(driveInfos.Select((driveInfo) => driveInfo.RootDirectory.FullName).ToList(), false).ConfigureAwait(false);
    }

    private void ReplaceArchiveWithExtractedContents(FileSystemTreeElement archiveFileSystemTreeElement, DirectoryInfo archiveExtractionDirectory)
    {
      if (archiveFileSystemTreeElement.ParentFileSystemTreeElement.IsDirectory)
      {
        FileSystemTreeElement parent = archiveFileSystemTreeElement.ParentFileSystemTreeElement;
        ObservableCollection<FileSystemTreeElement> parentContainigFileSystemElementCollection = parent.ChildFileSystemTreeElements;
        int archiveFileIndex = parentContainigFileSystemElementCollection.IndexOf(archiveFileSystemTreeElement);
        var archiveRepresentationNode = new FileSystemTreeElement(
          parent.RootFileSystemTreeElement,
          parent,
          archiveExtractionDirectory) {IsArchive = true, AlternativeIElementName = archiveFileSystemTreeElement.ElementInfo.Name.Equals(archiveExtractionDirectory.Name, StringComparison.OrdinalIgnoreCase) ?  "" : archiveExtractionDirectory.Name };

        List<FileSystemTreeElement> lazySubdirectories = archiveRepresentationNode.InitializeWithLazyTreeStructure();
        archiveRepresentationNode.SortChildren();
        FilterFileSystemTree(archiveRepresentationNode);

        Application.Current.Dispatcher.Invoke(
          () =>
          {
            parentContainigFileSystemElementCollection.Insert(archiveFileIndex, archiveRepresentationNode);
            parentContainigFileSystemElementCollection.Remove(archiveFileSystemTreeElement);
            archiveFileSystemTreeElement.ParentFileSystemTreeElement.ChildFileSystemTreeElements =
              parentContainigFileSystemElementCollection;
            if (archiveRepresentationNode.IsLazyLoading)
            {
              ObserveVirtualDirectories(lazySubdirectories);
            }
            archiveRepresentationNode.IsExpanded = true;
          },
          DispatcherPriority.Send);
      }
    }

    public void SetIsBusy()
    {
      lock (this.syncLock)
      {
        this.busyCount = this.busyCount < 0 ? 1 : ++this.busyCount;
        this.IsBusy = this.busyCount > 0;
      }
    }

    public void ClearIsBusy()
    {
      lock (this.syncLock)
      {
        this.IsBusy = --this.busyCount > 0;
      }
    }

    public void SetIsExtracting()
    {
      lock (this.syncLock)
      {
        this.runningExtractionsCount = this.runningExtractionsCount < 0 ? 1 : ++this.runningExtractionsCount;
        this.HasExtractionsRunning = this.runningExtractionsCount > 0;
      }
    }

    public void ClearIsExtracting()
    {
      lock (this.syncLock)
      {
        this.HasExtractionsRunning = --this.runningExtractionsCount > 0;
      }
    }

    private async void UpdateFileExplorerSourceOnRawCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) => await AddFilesAsync(e.NewItems.Cast<string>().ToList(), true).ConfigureAwait(false);

    public async Task AddFilesAsync(List<string> newFilePaths, bool isRootFolderExpanded)
    {
      if (!newFilePaths?.Any() ?? true)
      {
        return;
      }

      await Task.Run(() =>
      {
        SetIsBusy();

        if (this.IsReplacingFilesOnAddNew)
        {
          ClearFileSystemTree();
        }

        foreach (string path in newFilePaths)
        {
          FileSystemInfo newFileSystemInfo = null;
          if (File.Exists(path))
          {
            newFileSystemInfo = new FileInfo(path);
          }
          else if (Directory.Exists(path))
          {
            newFileSystemInfo = new DirectoryInfo(path);
          }

          if (newFileSystemInfo != null)
          {
            AddFilePathInfoToExplorerTree(newFileSystemInfo, isRootFolderExpanded);
          }

          ClearIsBusy();
          this.isInitializing = false;
        }
        this.VirtualExplorerRootDirectory.SortChildren();
      }).ConfigureAwait(false);
    }

    public void ClearFileSystemTree()
    {
      this.VirtualExplorerRootDirectory.ChildFileSystemTreeElements
        .Where((fileSystemTreeElement) => !fileSystemTreeElement.IsSystemDirectory)
        .ToList()
        .ForEach(
          (fileSystemTreeElement) =>
          {
            this.VirtualExplorerRootDirectory.ChildFileSystemTreeElements.Remove(fileSystemTreeElement);
            fileSystemTreeElement.ExpandedChanged -= ToggleDragDropHintOnTopLevelDirectoryExpanded;
          });

      UpdateFileSystemElementTreeState(FileSystemTreeElement.EmptyNode);
    }

    public async Task<(bool IsSuccessful, DirectoryInfo DestinationDirectory)> ExtractArchive(FileSystemTreeElement archiveFileTreeElement)
    {
      if (archiveFileTreeElement.IsDirectory || !archiveFileTreeElement.ElementInfo.Exists)
      {
        return (false, new DirectoryInfo(@"c:\"));
      }

      SetIsBusy();
      var fileExtractor = new SharpCompressLibArchiveExtractor();
      lock (this.syncLock)
      {
        if (this.ArchiveExtractorInstanceTable.ContainsKey(archiveFileTreeElement))
        {
          ClearIsBusy();
          return (false, new DirectoryInfo(@"c:\"));
        }

        this.ArchiveExtractorInstanceTable.Add(archiveFileTreeElement, fileExtractor);
      }

      SetIsExtracting();

      var progressReporter = new Progress<ExtractionProgressEventArgs>(ReportExtractionProgress);

      (bool IsSuccessful, DirectoryInfo DestinationDirectory) extractionResult = await
        fileExtractor.ExtractZipArchiveAsync(archiveFileTreeElement.ElementInfo as FileInfo, progressReporter);

      // Replace archive file with directory representation in explorer after extraction completed
      if (extractionResult.IsSuccessful)
      {
        ReplaceArchiveWithExtractedContents(archiveFileTreeElement, extractionResult.DestinationDirectory);
      }

      CleanUpExtraction(archiveFileTreeElement);
      return extractionResult;
    }

    private void CleanUpExtraction(FileSystemTreeElement archiveFileTreeElement)
    {
      ClearIsExtracting();
      lock (this.syncLock)
      {
        this.HasExtractionsRunning = this.runningExtractionsCount > 0;
        this.ArchiveExtractorInstanceTable.Remove(archiveFileTreeElement);
        this.ExtractionProgressTable.Remove(archiveFileTreeElement.ElementInfo);
      }
      ClearIsBusy();
    }

    private void AddFilePathInfoToExplorerTree([NotNull] FileSystemInfo pathInfo, bool isRootFolderExpanded)
    {
      var fileSystemTreeElement = new FileSystemTreeElement(
        this.VirtualExplorerRootDirectory,
        this.VirtualExplorerRootDirectory,
        pathInfo);

      fileSystemTreeElement.IsArchive = !fileSystemTreeElement.IsDirectory
                                    && FileExtractor.FileIsArchive(fileSystemTreeElement.ElementInfo as FileInfo);

      fileSystemTreeElement.IsSystemDirectory = DriveInfo.GetDrives().Any((driveInfo) => driveInfo.RootDirectory.FullName.Equals(fileSystemTreeElement.ElementInfo.FullName, StringComparison.OrdinalIgnoreCase));

      if (fileSystemTreeElement.IsDirectory || fileSystemTreeElement.IsArchive)
      {
        List<FileSystemTreeElement> lazyFileSystemElements = fileSystemTreeElement.InitializeWithLazyTreeStructure();

        // Observe top level tree directories 'IsExpanded' to show/ hide drag drop hint accordingly
        ObserveTopLevelDirectoryIsExpanded(fileSystemTreeElement);

        // Observe lazy children
        if (fileSystemTreeElement.IsLazyLoading)
        {
          ObserveVirtualDirectories(lazyFileSystemElements);
          fileSystemTreeElement.IsExpanded = isRootFolderExpanded && !fileSystemTreeElement.IsArchive;
        }
      }

      // Validate state including the new item that to this point is still not added to the tree
      UpdateFileSystemElementTreeState(fileSystemTreeElement);

      Application.Current.Dispatcher.Invoke(
        () =>
        {
          FilterFileSystemTree(fileSystemTreeElement);
          this.VirtualExplorerRootDirectory.ChildFileSystemTreeElements.Add(fileSystemTreeElement);
        },
        DispatcherPriority.Send);
    }

    private void UpdateFileSystemElementTreeState(FileSystemTreeElement fileSystemTreeElement)
    {
      this.IsExplorerInDefaultState = (fileSystemTreeElement.IsEmptyElement || 
                                       fileSystemTreeElement.IsSystemDirectory && !fileSystemTreeElement.IsExpanded)
                                      && this.VirtualExplorerRootDirectory.ChildFileSystemTreeElements
                                        .All((topLevelElement) => topLevelElement.IsSystemDirectory 
                                                                  && !topLevelElement.IsExpanded 
                                                                  || !this.VirtualExplorerRootDirectory.ChildFileSystemTreeElements.Any());
    }

    public void ObserveTopLevelDirectoryIsExpanded(FileSystemTreeElement topLevelDirectory) => topLevelDirectory.ExpandedChanged += ToggleDragDropHintOnTopLevelDirectoryExpanded;

    private void ToggleDragDropHintOnTopLevelDirectoryExpanded(object sender, ValueChangedEventArgs<bool> e)
    {
      UpdateFileSystemElementTreeState(sender as FileSystemTreeElement);
    }

    public void ObserveVirtualDirectories(List<FileSystemTreeElement> virtualDirectories)
    {
      // Search for the lazy nodes
      foreach (FileSystemTreeElement virtualSubdirectory in virtualDirectories)
      {
        virtualSubdirectory.ExpandedChanged += LazyLoadChildrenOnLazyNodeExpanded;
      }
    }

    private void LazyLoadChildrenOnLazyNodeExpanded(object sender, ValueChangedEventArgs<bool> e)
    {
      if (sender is FileSystemTreeElement directoryNode
          && directoryNode.HasLazyChildren
          && e.NewValue)
      {
        directoryNode.ExpandedChanged -= LazyLoadChildrenOnLazyNodeExpanded;
        LazyLoadChildren(directoryNode);
      }
    }

    private void LazyLoadChildren(FileSystemTreeElement directoryNode)
    {
      if (!directoryNode.HasLazyChildren)
      {
        return;
      }

      directoryNode.HasLazyChildren = false;
      Task.Run(
        () =>
        {
          List<FileSystemTreeElement> lazyFileSystemElements = directoryNode.InitializeWithLazyTreeStructure();
          directoryNode.SortChildren();
          FilterFileSystemTree(directoryNode);

          if (directoryNode.IsLazyLoading)
          {
            ObserveVirtualDirectories(lazyFileSystemElements);
          }
        }).ConfigureAwait(true);
    }

    private void ReportExtractionProgress(ExtractionProgressEventArgs e)
    {
      Application.Current.Dispatcher.InvokeAsync(
        () =>
        {
          if (!this.ExtractionProgressTable.ContainsKey(e.ArchiveInfo))
          {
            this.ExtractionProgressTable.Add(e.ArchiveInfo, e);
            return;
          }

          if (e.PercentageRead <= this.ExtractionProgressTable[e.ArchiveInfo].PercentageRead)
          {
            return;
          }

          this.ExtractionProgressTable[e.ArchiveInfo] = e;
          this.HasExtractionsRunning = this.runningExtractionsCount > 0;
        }, DispatcherPriority.Render);
    }

    private bool CanExecuteRemoveFile(object obj)
    {
      return obj is FileSystemTreeElement fileSystemTreeElement && !fileSystemTreeElement.IsSystemDirectory;
    }

    private void ExecuteRemoveFile(object obj)
    {
      if (obj is FileSystemTreeElement fileSystemTreeElement)
      {
        fileSystemTreeElement.ParentFileSystemTreeElement?.ChildFileSystemTreeElements?.Remove(fileSystemTreeElement);
        fileSystemTreeElement.ExpandedChanged -= ToggleDragDropHintOnTopLevelDirectoryExpanded;

        UpdateFileSystemElementTreeState(FileSystemTreeElement.EmptyNode);
      }
    }

    private void ExecuteFilterFilesByExtension(object obj)
    {
      FilterFileSystemTree();
    }

    private bool CanExecuteFilterFilesByExtension(object obj)
    {
      return this.IsFilteringFileSystemTreeByCustomExtensions && !string.IsNullOrWhiteSpace(this.FileFilterExtensions);
    }

    [NotifyPropertyChangedInvocator]
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void FilterFileSystemTree()
    {
      if (this.VirtualExplorerRootDirectory?.ChildFileSystemTreeElements?.Any() ?? false)
      {
        this.VirtualExplorerRootDirectory.ApplyActionOnSubTree(ApplyFilterOnFileSystemTree, (element) => true);
      }
    }

    public void FilterFileSystemTree(FileSystemTreeElement rootFileSystemTreeElement)
    {
      rootFileSystemTreeElement.ApplyActionOnSubTree(ApplyFilterOnFileSystemTree, (element) => true);
    }

    private void ApplyFilterOnFileSystemTree(FileSystemTreeElement fileSystemTreeElement)
    {
      if (fileSystemTreeElement.ElementInfo is DirectoryInfo || fileSystemTreeElement.IsEmptyElement)
      {
        fileSystemTreeElement.IsVisible = true;
        return;
      }

      if (this.IsFilteringFileSystemTreeByCustomExtensions)
      {
        ApplyCustomFilters(fileSystemTreeElement);
        return;
      }

      ApplyDefaultFilters(fileSystemTreeElement);
    }

    private void ApplyCustomFilters(FileSystemTreeElement fileSystemTreeElement)
    {
      fileSystemTreeElement.IsVisible = fileSystemTreeElement.ElementInfo.Extension.Length > 1;
      List<string> splitExtensions = this.FileFilterExtensions?
              .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
              .ToList()
              .Select((filterFileExtension) => filterFileExtension.TrimStart(' ', '.').TrimEnd()).ToList() ?? new List<string>();

      if (splitExtensions.Any(
        (fileExtension) => fileExtension.Equals("*", StringComparison.OrdinalIgnoreCase)))
      {
        fileSystemTreeElement.IsVisible = 
          fileSystemTreeElement.ElementInfo.Extension.Length > 1 
          && !splitExtensions.Any(
            (extension) => extension.Equals(fileSystemTreeElement.ElementInfo.Extension.Substring(1),
                                              StringComparison.OrdinalIgnoreCase));
        return;
      }

      fileSystemTreeElement.IsVisible = 
        fileSystemTreeElement.ElementInfo.Extension.Length > 1 
        && splitExtensions.Any(
          (extension) => extension.Equals(fileSystemTreeElement.ElementInfo.Extension.Substring(1),
                                            StringComparison.OrdinalIgnoreCase));
    }

    private void ApplyDefaultFilters(FileSystemTreeElement fileSystemTreeElement)
    {
      if (fileSystemTreeElement.ElementInfo.Extension.Length < 2)
      {
        fileSystemTreeElement.IsVisible = this.defaultFilterFileExtensions.HasFlag(FileExtensions.Any);
        return;
      }

      if (!this.IsShowingArchiveFiles && fileSystemTreeElement.IsArchive)
      {
        fileSystemTreeElement.IsVisible = false;
        return;
      }

      if (!this.IsShowingTxtFiles && fileSystemTreeElement.ElementInfo.Extension.Substring(1).Equals(
            FileExtensions.Txt.ToString("G"), StringComparison.OrdinalIgnoreCase))
      {
        fileSystemTreeElement.IsVisible = false;
        return;
      }

      if (!this.IsShowingIniFiles && fileSystemTreeElement.ElementInfo.Extension.Substring(1).Equals(
            FileExtensions.Ini.ToString("G"), StringComparison.OrdinalIgnoreCase))
      {
        fileSystemTreeElement.IsVisible = false;
        return;
      }

      if (!this.IsShowingLogFiles && fileSystemTreeElement.ElementInfo.Extension.Substring(1).Equals(
            FileExtensions.Log.ToString("G"), StringComparison.OrdinalIgnoreCase))
      {
        fileSystemTreeElement.IsVisible = false;
        return;
      }

      fileSystemTreeElement.IsVisible = this.IsShowingLogFiles
                                        && (fileSystemTreeElement.ElementInfo.Extension.Substring(1).Equals(
                                              FileExtensions.Log.ToString("G"),
                                              StringComparison.OrdinalIgnoreCase)
                                            || fileSystemTreeElement.ElementInfo.Extension.Substring(1).Equals(
                                              FileExtensions.Bak.ToString("G"),
                                              StringComparison.OrdinalIgnoreCase))
                                        || this.IsShowingTxtFiles
                                            && fileSystemTreeElement.ElementInfo.Extension.Substring(1).Equals(
                                          FileExtensions.Txt.ToString("G"),
                                          StringComparison.OrdinalIgnoreCase)
                                        || this.IsShowingIniFiles
                                            && fileSystemTreeElement.ElementInfo.Extension.Substring(1).Equals(
                                          FileExtensions.Ini.ToString("G"),
                                          StringComparison.OrdinalIgnoreCase)
                                        || this.IsShowingArchiveFiles
                                            && fileSystemTreeElement.IsArchive
                                        || this.IsShowingAnyFiles;
    }

    private bool AreAllDefaultFiltersDisabled()
    {
      return !this.IsShowingLogFiles && !this.IsShowingTxtFiles && !this.IsShowingArchiveFiles && !this.IsShowingIniFiles;
    }

    private void DisableAllFiltersButAnyFilter()
    {
      this.IsShowingLogFiles = false;
      this.IsShowingTxtFiles = false;
      this.IsShowingArchiveFiles = false;
      this.IsShowingIniFiles = false;
    }

    private void EnableAllFilters()
    {
      this.IsShowingLogFiles = true;
      this.IsShowingTxtFiles = true;
      this.IsShowingArchiveFiles = true;
      this.IsShowingIniFiles = true;
    }

    public event PropertyChangedEventHandler PropertyChanged;
    public ICommand RemoveFileCommand => new AsyncRelayCommand(ExecuteRemoveFile, CanExecuteRemoveFile);
    public ICommand FilterFilesByCustomExtensionsCommand => new AsyncRelayCommand(ExecuteFilterFilesByExtension, (param) => !string.IsNullOrWhiteSpace(this.FileFilterExtensions) && this.IsFilteringFileSystemTreeByCustomExtensions);
    public ICommand FilterFilesByDefaultExtensionsCommand => new AsyncRelayCommand(ExecuteFilterFilesByExtension, (param) =>  !this.IsFilteringFileSystemTreeByCustomExtensions);

    private int busyCount;
    private int runningExtractionsCount;
    private bool isInitializing;
    private FileExtensions defaultFilterFileExtensions;

    private readonly object syncLock = new object();

    private string fileFilterExtensions;   
    public string FileFilterExtensions
    {
      get { return this.fileFilterExtensions; }
      set 
      { 
        this.fileFilterExtensions = value;
        OnPropertyChanged();
      }
    }


    private bool isExtensionFilterEnabled;
    public bool IsExtensionFilterEnabled
    {
      get => this.isExtensionFilterEnabled;
      set
      {
        if (value == this.isExtensionFilterEnabled)
        {
          return;
        }
        this.isExtensionFilterEnabled = value;
        OnPropertyChanged();
      }
    }

    private FileSystemTreeElement virtualExplorerRootDirectory;
    public FileSystemTreeElement VirtualExplorerRootDirectory
    {
      get { return this.virtualExplorerRootDirectory; }
      private set
      {
        this.virtualExplorerRootDirectory = value;
        OnPropertyChanged();
      }
    }

    private ObservableCollection<string> rawFilePaths;
    public ObservableCollection<string> RawFilePaths
    {
      get => this.rawFilePaths;
      set
      {
        if (this.RawFilePaths != null)
        {
          this.RawFilePaths.CollectionChanged -= UpdateFileExplorerSourceOnRawCollectionChanged;
        }

        this.rawFilePaths = value ?? new ObservableCollection<string>();
        this.RawFilePaths.CollectionChanged += UpdateFileExplorerSourceOnRawCollectionChanged;
        OnPropertyChanged();

        if (this.RawFilePaths.Any())
        {
          AddFilesAsync(this.RawFilePaths.ToList(), true);
        }
      }
    }

    private bool isReplacingFilesOnAddNew;
    public bool IsReplacingFilesOnAddNew
    {
      get { return this.isReplacingFilesOnAddNew; }
      set
      {
        this.isReplacingFilesOnAddNew = value;
        OnPropertyChanged();
      }
    }

    private bool isBusy;
    public bool IsBusy
    {
      get { return this.isBusy; }
      private set
      {
        this.isBusy = value;
        OnPropertyChanged();
      }
    }

    private bool isDeleteExtractedFilesOnExitEnabled;
    public bool IsDeleteExtractedFilesOnExitEnabled
    {
      get { return this.isDeleteExtractedFilesOnExitEnabled; }
      set
      {
        this.isDeleteExtractedFilesOnExitEnabled = value;
        OnPropertyChanged();
      }
    }

    private ObservableDictionary<string, Color> fileInfoIdColorMap;
    public ObservableDictionary<string, Color> FileInfoIdColorMap
    {
      get { return this.fileInfoIdColorMap; }
      set
      {
        this.fileInfoIdColorMap = value;
        OnPropertyChanged();
      }
    }

    private ObservableDictionary<FileSystemTreeElement, IFileExtractor> archiveExtractorInstanceTable;   
    public ObservableDictionary<FileSystemTreeElement, IFileExtractor> ArchiveExtractorInstanceTable
    {
      get => this.archiveExtractorInstanceTable;
      private set 
      { 
        this.archiveExtractorInstanceTable = value; 
        OnPropertyChanged();
      }
    }

    private ObservableDictionary<FileSystemInfo, ExtractionProgressEventArgs> extractionProgressTable;   
    public ObservableDictionary<FileSystemInfo, ExtractionProgressEventArgs> ExtractionProgressTable
    {
      get { return this.extractionProgressTable; }
      set 
      { 
        this.extractionProgressTable = value; 
        OnPropertyChanged();
      }
    }

    private bool hasExtractionsRunning;   
    public bool HasExtractionsRunning
    {
      get { return this.hasExtractionsRunning; }
      private set 
      { 
        this.hasExtractionsRunning = value; 
        OnPropertyChanged();
      }
    }

    private bool isExplorerInDefaultState;   
    public bool IsExplorerInDefaultState
    {
      get { return this.isExplorerInDefaultState; }
      set 
      { 
        this.isExplorerInDefaultState = value; 
        OnPropertyChanged();
      }
    }

    private bool isFilteringFileSystemTreeByCustomExtensions;   
    public bool IsFilteringFileSystemTreeByCustomExtensions
    {
      get { return this.isFilteringFileSystemTreeByCustomExtensions; }
      set
      {
        this.isFilteringFileSystemTreeByCustomExtensions = value;
        OnPropertyChanged();
        FilterFileSystemTree();
      }
    }

    private bool isShowingLogFiles;
    public bool IsShowingLogFiles
    {
      get => this.isShowingLogFiles;
      set
      {
        if (value == this.isShowingLogFiles)
        {
          return;
        }
        this.isShowingLogFiles = value;
        OnPropertyChanged();
      }
    }

    private bool isShowingTxtFiles;
    public bool IsShowingTxtFiles
    {
      get => this.isShowingTxtFiles;
      set
      {
        if (value == this.isShowingTxtFiles)
        {
          return;
        }
        this.isShowingTxtFiles = value;
        OnPropertyChanged();
      }
    }

    private bool isShowingArchiveFiles;

    public bool IsShowingArchiveFiles
    {
      get => this.isShowingArchiveFiles;
      set
      {
        if (value == this.isShowingArchiveFiles)
        {
          return;
        }
        this.isShowingArchiveFiles = value;
        OnPropertyChanged();
      }
    }

    private bool isShowingIniFiles;
    public bool IsShowingIniFiles
    {
      get => this.isShowingIniFiles;
      set
      {
        if (value == this.isShowingIniFiles)
        {
          return;
        }
        this.isShowingIniFiles = value;
        OnPropertyChanged();
      }
    }

    private bool isShowingAnyFiles;
    public bool IsShowingAnyFiles
    {
      get => this.isShowingAnyFiles;
      set
      {
        if (value == this.isShowingAnyFiles)
        {
          return;
        }

        if (AreAllDefaultFiltersDisabled() && !value)
        {
          this.isShowingAnyFiles = true;
          OnPropertyChanged();
          EnableAllFilters();
          return;
        }

        this.isShowingAnyFiles = value;
        OnPropertyChanged();

        if (this.IsShowingAnyFiles)
        {
          EnableAllFilters();
        }
      }
    }
  }
}
