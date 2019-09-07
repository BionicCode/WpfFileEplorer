using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using BionicLibrary.Net.Utility;
using FileExplorer.Wpf.ViewModel;
using FileExplorer.Wpf.Zip;
using WpfControls = System.Windows.Controls;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using UserControl = System.Windows.Controls.UserControl;
using System.Windows.Forms;

namespace FileExplorer.Wpf.View
{
  /// <summary>
  /// Interaction logic for FileExplorer.xaml
  /// </summary>
  [TemplatePart(Name = "PART_ExplorerTreeView", Type = typeof(WpfControls.TreeView))]
  public partial class FileExplorer : UserControl, ICommandSource
  {
    #region routed events

    #region ExplorerSourceChangedRoutedEvent
    /// <summary>
    /// Raised when new items are added to the items source. Strategy Bubble
    /// </summary>
    public static readonly RoutedEvent ExplorerSourceChangedRoutedEvent = EventManager.RegisterRoutedEvent("ExplorerSourceChanged",
      RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(FileExplorer));

    public event RoutedEventHandler ExplorerSourceChanged
    {
      add { AddHandler(FileExplorer.ExplorerSourceChangedRoutedEvent, value); }
      remove { RemoveHandler(FileExplorer.ExplorerSourceChangedRoutedEvent, value); }
    }

    #endregion

    #region PreviewExplorerSourceChangedRoutedEvent

    /// <summary>
    /// Raised when new items are added to the items source. Strategy Tunnel
    /// </summary>
    public static readonly RoutedEvent PreviewExplorerSourceChangedRoutedEvent = EventManager.RegisterRoutedEvent("PreviewExplorerSourceChanged",
      RoutingStrategy.Tunnel, typeof(RoutedEventHandler), typeof(FileExplorer));

    public event RoutedEventHandler PreviewExplorerSourceChanged
    {
      add { AddHandler(FileExplorer.PreviewExplorerSourceChangedRoutedEvent, value); }
      remove { RemoveHandler(FileExplorer.PreviewExplorerSourceChangedRoutedEvent, value); }
    }

    #endregion

    #region ExplorerSourceClearedRoutedEvent

    public static readonly RoutedEvent ExplorerSourceClearedRoutedEvent = EventManager.RegisterRoutedEvent("ExplorerCleared",
      RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(FileExplorer));

    public event RoutedEventHandler ExplorerCleared
    {
      add { AddHandler(FileExplorer.ExplorerSourceClearedRoutedEvent, value); }
      remove { RemoveHandler(FileExplorer.ExplorerSourceClearedRoutedEvent, value); }
    }

    #endregion

    #region PreviewExplorerClearedRoutedEvent

    public static readonly RoutedEvent PreviewExplorerClearedRoutedEvent = EventManager.RegisterRoutedEvent("PreviewExplorerCleared",
      RoutingStrategy.Tunnel, typeof(RoutedEventHandler), typeof(FileExplorer));

    public event RoutedEventHandler PreviewExplorerCleared
    {
      add { AddHandler(FileExplorer.PreviewExplorerClearedRoutedEvent, value); }
      remove { RemoveHandler(FileExplorer.PreviewExplorerClearedRoutedEvent, value); }
    }

    #endregion

    #endregion

    #region routed commands

    public static readonly RoutedUICommand ClearTreeViewRoutedCommand = new RoutedUICommand("Removes all files and folders from the view", nameof(FileExplorer.ClearTreeViewRoutedCommand), typeof(FileExplorer));
    public static readonly RoutedUICommand AddNewFoldersRoutedCommand = new RoutedUICommand("Opens the Windows Explorer for the user to choose folders to add to the view", nameof(FileExplorer.AddNewFoldersRoutedCommand), typeof(FileExplorer));
    public static readonly RoutedUICommand AddNewFilesRoutedCommand = new RoutedUICommand("Opens the Windows Explorer for the user to choose files to add to the view", nameof(FileExplorer.AddNewFilesRoutedCommand), typeof(FileExplorer));
    public static readonly RoutedUICommand ViewFileRoutedCommand = new RoutedUICommand("Opens a ZIP file", nameof(FileExplorer.ViewFileRoutedCommand), typeof(FileExplorer));
    public static readonly RoutedUICommand OpenDocumentInExternalEditorRoutedCommand = new RoutedUICommand("Opens the systems default editor to open the document. It first tries to open it file extension specific but if no appropriate application available, the document is opened as .txt file", nameof(FileExplorer.OpenDocumentInExternalEditorRoutedCommand), typeof(FileExplorer));

    #endregion

    #region command handlers

    private void CanExecuteAddNewFiles(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = true;
    private void ExecuteAddNewFiles(object sender, ExecutedRoutedEventArgs e) => OpenWindowsFileExplorerAsync();

    private void CanExecuteAddNewFolders(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = true;
    private void ExecuteAddNewFolders(object sender, ExecutedRoutedEventArgs e) => OpenWindowsFormsFolderDialogAsync();

    private void CanExecuteClearTreeView(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = this.PART_ExplorerTreeView?.HasItems ?? false;
    private void ExecuteClearTreeView(object sender, ExecutedRoutedEventArgs e) => ClearExplorerSource();

    private void ExecuteViewFile(object sender, ExecutedRoutedEventArgs e)
    {
      if (!(e.OriginalSource is FrameworkElement frameworkElement))
      {
        return;
      }

      if (!(frameworkElement.DataContext is FileSystemTreeElement fileSystemTreeElement) || fileSystemTreeElement.IsDirectory)
      {
        return;
      }

      // If file is a compressed archive, then try to extract to temp folder and open the destination folder ...
      if (FileExtractor.FileIsArchive(fileSystemTreeElement.ElementInfo as FileInfo))
      {
        this.ExplorerViewModel.ExtractArchive(fileSystemTreeElement).ConfigureAwait(true);
        return;
      }

      // ... or just open the document if it's no archive but file
      throw new NotImplementedException();
    }

    private void CanExecuteViewFile(object sender, CanExecuteRoutedEventArgs e)
    {
      e.CanExecute = true;
    }

    private void ExecuteOpenFileInExternalEditor(object sender, ExecutedRoutedEventArgs e)
    {
      FileInfo fileInfo = (this.PART_ExplorerTreeView.SelectedItem as FileSystemTreeElement)?.ChildFileSystemTreeElements.OfType<FileInfo>().FirstOrDefault();
      if (fileInfo != null && fileInfo.Exists)
      {
        try
        {
          Process.Start(fileInfo.FullName);
        }
        catch (Win32Exception)
        {
        }
      }
    }

    private void CanExecuteOpenFileInExternalEditor(object sender, CanExecuteRoutedEventArgs e)
    {
      e.CanExecute = true;
    }

    private void ExecuteCollapseDirectories(object sender, ExecutedRoutedEventArgs e)
    {
      ToggleDirectoryNodeRecursive(this.ExplorerViewModel.VirtualExplorerRootDirectory, false);
    }

    private void ToggleDirectoryNodeRecursive(FileSystemTreeElement fileSystemTreeElement, bool isExpanded)
    {
      fileSystemTreeElement.IsExpanded = isExpanded;
      fileSystemTreeElement.ChildFileSystemTreeElements.Where((element) => element.IsDirectory).ToList().ForEach((node) => ToggleDirectoryNodeRecursive(node, isExpanded));
    }

    private void CanExecuteCollapseDirectories(object sender, CanExecuteRoutedEventArgs e)
    {
      e.CanExecute = this.ExplorerViewModel.VirtualExplorerRootDirectory.ChildFileSystemTreeElements.Any();
    }

    #endregion

    #region Commands

    public static ICommand AddFoldersCommand => new AsyncRelayCommand((param) => FileExplorer.Instance.ExecuteAddNewFolders(param, null));
    public static ICommand AddFilesCommand => new AsyncRelayCommand((param) => FileExplorer.Instance.ExecuteAddNewFiles(param, null));

    #endregion

    #region dependency properties

    public static readonly DependencyProperty HasProgressReportProperty = DependencyProperty.Register(
      "HasProgressReport",
      typeof(bool),
      typeof(FileExplorer),
      new PropertyMetadata(default(bool)));

    public bool HasProgressReport { get { return (bool)GetValue(FileExplorer.HasProgressReportProperty); } set { SetValue(FileExplorer.HasProgressReportProperty, value); } }

    public static readonly DependencyProperty IsClearExtractedFilesOnExitEnabledProperty = DependencyProperty.Register(
      "IsClearExtractedFilesOnExitEnabled",
      typeof(bool),
      typeof(FileExplorer),
      new PropertyMetadata(default(bool)));

    public bool IsClearExtractedFilesOnExitEnabled { get { return (bool) GetValue(FileExplorer.IsClearExtractedFilesOnExitEnabledProperty); } set { SetValue(FileExplorer.IsClearExtractedFilesOnExitEnabledProperty, value); } }

    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
      "Source",
      typeof(ObservableCollection<object>),
      typeof(FileExplorer),
      new PropertyMetadata(default(ObservableCollection<object>)));


    public ObservableCollection<object> Source { get { return (ObservableCollection<object>) GetValue(FileExplorer.SourceProperty); } set { SetValue(FileExplorer.SourceProperty, value); } }

    #endregion

    private FileExplorer()
    {
      InitializeComponent();
      this.RecentFolder = null;
      this.Loaded += (s, e) =>
      {
        this.PART_ExplorerTreeView.KeyUp += (sender, eventArgs) => OnKeyUp(eventArgs);
        this.DataContextChanged += ObserveVirtualRoot;
        ObserveVirtualRoot(this, new DependencyPropertyChangedEventArgs(FrameworkElement.DataContextProperty, null, this.DataContext));
      };

      //LogDocumentManager.DocumentLoading += (s, e) => this.HasProgressReport = true;
      //LogDocumentManager.DocumentLoaded += (s, e) => this.HasProgressReport = false;
    }

    private void ObserveVirtualRoot(object sender, DependencyPropertyChangedEventArgs e)
    {
      if (e.OldValue is FileExplorerViewModel)
      {
        this.ExplorerViewModel.VirtualExplorerRootDirectory.ChildFileSystemTreeElements.CollectionChanged -= NotifyOnExplorerSourceChanged;
      }

      if (e.NewValue is FileExplorerViewModel)
      {
        this.ExplorerViewModel = e.NewValue as FileExplorerViewModel;
        this.ExplorerViewModel.VirtualExplorerRootDirectory.ChildFileSystemTreeElements.CollectionChanged += NotifyOnExplorerSourceChanged;
      }
    }

    private void ClearExplorerSource()
    {
      this.ExplorerViewModel?.ClearFileSystemTree();
      FileExtractor.CleanUpExtractedTemporaryFiles();

      NotifyExplorerSourceCleared();
    }

    private async Task OpenWindowsFormsFolderDialogAsync()
    {
      using (var openFolderDialog = new FolderBrowserDialog())
      {
        if (this.RecentFolder?.Exists ?? false)
        {
          openFolderDialog.SelectedPath = this.RecentFolder.FullName;
        }

        if (openFolderDialog.ShowDialog() == DialogResult.OK)
        {
          await AddNewFilesToTree(new[] {openFolderDialog.SelectedPath});
          this.RecentFolder = new DirectoryInfo(openFolderDialog?.SelectedPath);
        }
      }
    }

    private async Task OpenWindowsFileExplorerAsync()
    {
      var windowsExplorer = new OpenFileDialog()
      {
        CheckFileExists = true,
        CheckPathExists = true,
        AddExtension = true,
        Multiselect = true,
        Filter = "Viewer Files|*.log;*.zip;*.rar;*.gz;*.tar;*.ini;*.bak;*.txt;*.xml;*.csv|All Files|*.*|Log Files|*.log;*.bak|Archive Files|*.zip;*.rar;*.gz;*.tar|Ini Files|*.ini|Text Files|*.txt;*.log;*.ini|Bak Files|*.bak"
      };

      if (windowsExplorer.ShowDialog() ?? false)
      {
        await AddNewFilesToTree(windowsExplorer.FileNames);
      }
    }

    private async Task AddNewFilesToTree(IEnumerable<string> fileNames)
    {
      await this.ExplorerViewModel.AddFilesAsync(fileNames.ToList(), true);
      //Task.Run(() => (sender as OpenFileDialog)?.FileNames
      //  .ToList()
      //  .ForEach(this.ExplorerViewModel.RawFilePaths.Add));
    }

    #region Overrides of FrameworkElement

    public override void OnApplyTemplate()
    {
      base.OnApplyTemplate();
      this.PART_ExplorerTreeView = GetTemplateChild("PART_ExplorerTreeView") as WpfControls.TreeView;
      if (this.PART_ExplorerTreeView == null)
      {
        throw new InvalidOperationException($"Template part {nameof(this.PART_ExplorerTreeView)} not found");
      }

      this.PART_ExplorerTreeView.KeyUp += (sender, eventArgs) => OnKeyUp(eventArgs);
    }

    #endregion

    #region Overrides of UIElement

    protected override async void OnKeyUp(KeyEventArgs e)
    {
      base.OnKeyUp(e);
      // Open file shortcut
      if (e.Key.Equals(Key.F) && e.KeyboardDevice.Modifiers.Equals(ModifierKeys.Alt))
      {
        await OpenWindowsFileExplorerAsync();
      }

      // OpenFolderDialog directory shrottcut
      if (e.Key.Equals(Key.D) && e.KeyboardDevice.Modifiers.Equals(ModifierKeys.Alt))
      {
        await OpenWindowsFormsFolderDialogAsync();
      }

      // Clear explorer shrottcut
      if (e.Key.Equals(Key.C) && e.KeyboardDevice.Modifiers.Equals(ModifierKeys.Alt))
      {
        await OpenWindowsFileExplorerAsync();
      }
    }

    #endregion

    private void NotifyOnExplorerSourceChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
      RaiseEvent(new RoutedEventArgs(FileExplorer.ExplorerSourceChangedRoutedEvent));
      RaiseEvent(new RoutedEventArgs(FileExplorer.PreviewExplorerSourceChangedRoutedEvent));
    }

    private void NotifyExplorerSourceCleared()
    {
      RaiseEvent(new RoutedEventArgs(FileExplorer.ExplorerSourceClearedRoutedEvent, this));
      RaiseEvent(new RoutedEventArgs(FileExplorer.PreviewExplorerClearedRoutedEvent, this));
    }

    private static FileExplorer instance;
    public static FileExplorer Instance => FileExplorer.instance != null ? FileExplorer.instance : (FileExplorer.instance = new FileExplorer());
    public IFileExplorerViewModel ExplorerViewModel { get; private set; }
    private DirectoryInfo RecentFolder { get; set; }
    private object SyncLock { get; } = new object();
    private WpfControls.TreeView PART_ExplorerTreeView { get; set; }


    #region Implementation of ICommandSource

    /// <summary>
    /// Command will be executed to view (open) files.
    /// </summary>
    public ICommand Command { get; }
    public object CommandParameter { get; }
    public IInputElement CommandTarget { get; }


    #endregion
  }
}
