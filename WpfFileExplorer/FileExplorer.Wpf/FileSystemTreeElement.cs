using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Windows;
using System.Windows.Threading;
using BionicLibrary.NetStandard.Generic;
using BionicLibrary.NetStandard.IO;
using FileExplorer.Wpf.Zip;
using JetBrains.Annotations;

namespace FileExplorer.Wpf
{
  /// <summary>
  /// A elementInfo object in the directory tree that describes a folder and its associated files
  /// <para/>
  /// and can be used as a model of several view items e.g. TreeViewItem or ListViewItem, since it supports binding properties like <see cref="IsExpanded"/> or <see cref="IsSelected"/>
  /// <para/>
  /// According to the invoked constructor the tree is automatically created from the single <see cref="ElementInfo"/> argument or explicitly constructed.
  /// </summary>
  public class FileSystemTreeElement : INotifyPropertyChanged, IComparable<FileSystemTreeElement>, IComparer<FileSystemTreeElement>, IComparer
  {
    public static FileSystemTreeElement EmptyNode => new FileSystemTreeElement() { IsEmptyElement = true };
    public static FileSystemTreeElement EmptyDirectoryNode => new FileSystemTreeElement() { IsEmptyElement = true, IsDirectory = true};

    /// A default value for preload depth of file system tree.
    /// Min value must be 2 in order to enable lazy loading
    /// </summary>
    public const int DefaultPreloadFileSystemTreedDepth = 2;

    protected FileSystemTreeElement()
    {
      this.IsVisible = true;
      this.FileSystemTreePreLoadDepth = -1;
      this.ChildFileSystemTreeElements = new ObservableCollection<FileSystemTreeElement>();
      this.IsEmptyElement = false;
      this.IsLazyLoading = true;
      this.ParentFileSystemTreeElement = null;
      this.ElementInfo = null;
      this.IsArchive = false;
    }

    /// <summary>
    /// Default consrtructor that gives full control over the tree srtucture
    /// </summary>
    /// <param name="parentFileSystemTreeElement">Parent FileSystemTreeElement</param>
    /// <param name="node">A elementInfo object in the directory tree that describes a folder and its associated files</param>
    /// <param name="childElements">A collection of <see cref="FileSystemTreeElement"/>s describing the subdirectories contained by this elementInfo object</param>
    public FileSystemTreeElement(FileSystemTreeElement parentFileSystemTreeElement, FileSystemInfo fileSystemElementInfo, IEnumerable<FileSystemTreeElement> childElements) : this()
    {
      this.ParentFileSystemTreeElement = parentFileSystemTreeElement;
      this.ElementInfo = fileSystemElementInfo;
      this.ChildFileSystemTreeElements = new ObservableCollection<FileSystemTreeElement>(childElements);
      this.isExpanded = false;
      this.isSelected = false;
    }


    /// <summary>
    /// Full automatic constructor that creates the full tree where <param name="elementInfo"></param> is the root. <para/>
    /// It populates the <see cref="SubdirectoryInfos"/> and the <see cref="ChildFileSystemTreeElements"/> property which describes the files contained by the current elementInfo directory.
    /// </summary>
    /// <param name="elementInfo">A elementInfo object in the directory tree that describes a folder and its associated files. Subdirectory and file info are created automatically from the elementInfo where this elementInfo is the root.</param>
    /// <param name="fileExtensionFilter">A flagged enum to filter the files to collect. Use FileExtensions.Any to collect all file types.</param>
    private FileSystemTreeElement(DirectoryInfo elementInfo) : this()
    {
      this.ElementInfo = elementInfo;
    }

    /// <summary>
    /// Semi-Automatic constructor that  populates the <see cref="ChildFileSystemTreeElements"/> property which describes the files contained by the current <param name="node"></param> directory.
    /// </summary>
    /// <param name="parentFileSystemTreeElement">Parent FileSystemTreeElement</param>
    /// <param name="elementInfoInfo">A elementInfo object in the directory tree that describes a folder and its associated files</param>
    /// <param name="childElements">A collection of <see cref="FileSystemTreeElement"/>s describing the subdirectories contained by this elementInfo object</param>
    /// <param name="fileExtensionFilter">A flagged enum to filter the files to collect. Use FileExtensions.Any to collect all file types.</param>
    public FileSystemTreeElement(
      FileSystemTreeElement rootFileSystemTreeElement,
      FileSystemTreeElement parentFileSystemTreeElement,
      FileSystemInfo elementInfoInfo,
      FileExtensions fileExtensionFilter = FileExtensions.Any) : this()
    {
      this.RootFileSystemTreeElement = rootFileSystemTreeElement;
      this.ParentFileSystemTreeElement = parentFileSystemTreeElement;
      this.ElementInfo = elementInfoInfo;
      this.FileExtensionFilter = fileExtensionFilter;
      this.isExpanded = false;
      this.isSelected = false;
    }

    public List<FileSystemTreeElement> InitializeWithLazyTreeStructure(int maxDepth = -1)
    {
      if (this.IsEmptyElement || !(this.ElementInfo is DirectoryInfo))
      {
        return new List<FileSystemTreeElement>();
      }

      int fileSystemTreDepth = Math.Max(this.FileSystemTreePreLoadDepth, maxDepth);
      if (fileSystemTreDepth < 0)
      {
        fileSystemTreDepth = FileSystemTreeElement.DefaultPreloadFileSystemTreedDepth;
      }

      List<FileSystemTreeElement> createdLazyDirectories = new List<FileSystemTreeElement>();
      ReadFolderStructure(fileSystemTreDepth, ref createdLazyDirectories);
      return createdLazyDirectories;
    }

    public void SortChildren()
    {
      if (!this.ChildFileSystemTreeElements.Any())
      {
        return;
      }
      List<FileSystemTreeElement> unsortedElements = this.ChildFileSystemTreeElements.ToList();
      unsortedElements.Sort();
      Application.Current.Dispatcher.InvokeAsync(
        () =>
        {
          this.ChildFileSystemTreeElements.Clear();
          this.ChildFileSystemTreeElements = new ObservableCollection<FileSystemTreeElement>(unsortedElements);
          this.ChildFileSystemTreeElements.ToList().ForEach((child) => { child.SortChildren(); });
        },
        DispatcherPriority.Send);
    }

    // Uses preorder traversal
    public static bool TryGetDirectoryElementOf(FileSystemTreeElement currentTreeElement, out FileSystemTreeElement directoryTreeElement)
    {
      directoryTreeElement = FileSystemTreeElement.EmptyNode;

      if (currentTreeElement.IsDirectory)
      {
        directoryTreeElement = currentTreeElement;
        return true;
      }

      return FileSystemTreeElement.TryGetDirectoryElementOf(currentTreeElement.ParentFileSystemTreeElement, out directoryTreeElement);
    }
    
    // Preorder traversal
    public void ReadFolderStructure(int remainingFileSystemTreeEnumerationLevelCount, ref List<FileSystemTreeElement> lazyChildren)
    {
      --remainingFileSystemTreeEnumerationLevelCount;

      // System drives are always attributed as Directory, Hidden and System by Windows.
      // This requires exclusion otherwise drives would be disabled
      bool isSystemDrive = DriveInfo.GetDrives().FirstOrDefault((driveInfo) => driveInfo.Name.Equals(this.ElementInfo.FullName, StringComparison.OrdinalIgnoreCase)) != null;
        this.IsHidden = !isSystemDrive && !this.IsArchive && (this.ElementInfo.Attributes.HasFlag(FileAttributes.Hidden)
                         || this.ElementInfo.Attributes.HasFlag(FileAttributes.ReparsePoint)
                         || this.ElementInfo.Attributes.HasFlag(FileAttributes.Encrypted)
                         || this.ElementInfo.Attributes.HasFlag(FileAttributes.Offline)
                         || this.ElementInfo.Attributes.HasFlag(FileAttributes.ReadOnly));
        this.IsSystem = this.ElementInfo.Attributes.HasFlag(FileAttributes.System) && !isSystemDrive && !this.IsArchive;

      if (!this.IsExistingDirectory || this.IsSystem || this.IsHidden)
      {
        // Return without creating child nodes
        return;
      }

      if (remainingFileSystemTreeEnumerationLevelCount < 0 )
      {
        return;
      }

      try
      {
        CreateChildItems(remainingFileSystemTreeEnumerationLevelCount, ref lazyChildren);
      }
      catch (System.UnauthorizedAccessException)
      {
        return;
      }
      catch (DirectoryNotFoundException)
      {
        return;
      }
    }

    public void ApplyActionOnSubTree(Action<FileSystemTreeElement> action, Predicate<FileSystemTreeElement> predicate)
    {
      if (!predicate(this))
      {
        return;
      }

      action(this);

      this.ChildFileSystemTreeElements
        .ToList()
        .ForEach((childElement) => childElement?.ApplyActionOnSubTree(action, predicate));

      //this.ChildFileSystemTreeElements = new ObservableCollection<FileSystemTreeElement>(this.ChildFileSystemTreeElements.ToList());
    }

    private bool UserHasFileSystemElementPermission(FileSystemInfo infoToCheck)
    {
      AuthorizationRuleCollection accessRules = null;
      if (infoToCheck is FileInfo fileInfo && CheckFileAccessRights(fileInfo, out accessRules))
      {
        return CheckAccessRights(accessRules);
      }

      if (infoToCheck is DirectoryInfo directoryInfo && CheckDirectoryAccessRights(directoryInfo, out accessRules))
      {
        return CheckAccessRights(accessRules);
      }

      return false;
    }

    private bool CheckFileAccessRights(FileInfo fileInfo, out AuthorizationRuleCollection accessRules)
    {
      accessRules = fileInfo.GetAccessControl()?.GetAccessRules(true, true,
        typeof(System.Security.Principal.SecurityIdentifier));

      return accessRules != null;
    }

    private bool CheckDirectoryAccessRights(DirectoryInfo directoryInfo, out AuthorizationRuleCollection accessRules)
    {
      accessRules = directoryInfo.GetAccessControl()?.GetAccessRules(true, true,
        typeof(System.Security.Principal.SecurityIdentifier));

      return accessRules != null;
    }

    private bool CheckAccessRights(AuthorizationRuleCollection accessRules)
    {
      var isAccessAllowed = false;
      foreach (FileSystemAccessRule rule in accessRules)
      {
        if ((FileSystemRights.Write & rule.FileSystemRights) != FileSystemRights.Write)
        {
          continue;
        }

        isAccessAllowed |= rule.AccessControlType == AccessControlType.Allow;
      }

      return isAccessAllowed;
    }

    private void CreateChildItems(int remainingFileSystemTreeEnumerationLevelCount, ref List<FileSystemTreeElement> lazyChildren)
    {
      foreach (FileSystemInfo fileSystemTreeChildInfo in (this.ElementInfo as DirectoryInfo).EnumerateFileSystemInfos(
        "*",
        SearchOption.TopDirectoryOnly))
      {
        if (fileSystemTreeChildInfo is DirectoryInfo subdirectoryInfo)
        {
          var childDirectoryElement = new FileSystemTreeElement(this.RootFileSystemTreeElement, this, subdirectoryInfo) { HasLazyChildren = remainingFileSystemTreeEnumerationLevelCount.Equals(0) };

          Application.Current.Dispatcher.Invoke(
            () => this.ChildFileSystemTreeElements.Add(childDirectoryElement),
            DispatcherPriority.Send);

          if (childDirectoryElement.HasLazyChildren)
          {
            lazyChildren.Add(childDirectoryElement);
          }
          else
          {
            childDirectoryElement.ReadFolderStructure(remainingFileSystemTreeEnumerationLevelCount, ref lazyChildren);
          }
        }
        else if (fileSystemTreeChildInfo is FileInfo fileInfo)
        {
          var fileIsArchive = FileExtractor.FileIsArchive(fileInfo);
          var childFileElement = new FileSystemTreeElement(this.RootFileSystemTreeElement, this, fileInfo) {IsArchive = fileIsArchive};

          Application.Current.Dispatcher.Invoke(
            () => this.ChildFileSystemTreeElements.Add(childFileElement),
            DispatcherPriority.Send);
        }
      }
    }

    /// <summary>
    /// Sorts alphabetically ignoring the file exetension.
    /// System directories have precedence over directories and directories have precedence over files.
    /// </summary>
    /// <param name="treeElementA">First comparand</param>
    /// <param name="treeElementB">Second Comparand</param>
    /// <returns>'-1' when <paramref name="treeElementA"/> is bigger than <paramref name="treeElementB"/>,  '0' for euality and '1' when <paramref name="treeElementB"/> is bigger than <paramref name="treeElementA"/></returns>
    private int FileSystemTreeSortComparison(FileSystemTreeElement treeElementA, FileSystemTreeElement treeElementB)
    {
      if (treeElementA.IsSystemDirectory && !treeElementB.IsSystemDirectory)
      {
        return -1;
      }

      if (!treeElementA.IsSystemDirectory && treeElementB.IsSystemDirectory)
      {
        return 1;
      }

      if (treeElementA.IsDirectory && !treeElementB.IsDirectory)
      {
        return -1;
      }

      if (!treeElementA.IsDirectory && treeElementB.IsDirectory)
      {
        return 1;
      }

      string fileNameAWhithoutExtension = treeElementA.ElementInfo.Name.Substring(
        0,
        treeElementA.ElementInfo.Name.Length - treeElementA.ElementInfo.Extension.Length);
      string fileNameBWhithoutExtension = treeElementB.ElementInfo.Name.Substring(
        0,
        treeElementB.ElementInfo.Name.Length - treeElementB.ElementInfo.Extension.Length);

      int fileNameAFirstDotIndex = fileNameAWhithoutExtension.IndexOf(".", StringComparison.OrdinalIgnoreCase);
      int fileNameBFirstDotIndex = fileNameBWhithoutExtension.IndexOf(".", StringComparison.OrdinalIgnoreCase);

      // Both file names doesn't contain a dot separator (normal case)
      if (fileNameAFirstDotIndex.Equals(-1) && fileNameBFirstDotIndex.Equals(-1))
      {
        return CompareAlphabetically(treeElementA.ElementInfo.Name, treeElementB.ElementInfo.Name);
      }

      if (fileNameAFirstDotIndex.Equals(-1) || fileNameBFirstDotIndex.Equals(-1))
      {
        return CompareAlphabetically(fileNameAWhithoutExtension, fileNameBWhithoutExtension);
      }

      // If both names contain a dot prefix separator --> compare prefixes
      // File without this separator have precedence over those that contain one (on matching prefix)
      string prefixFileNameA = fileNameAWhithoutExtension.Substring(0, fileNameAFirstDotIndex);
      string prefixFileNameB = fileNameBWhithoutExtension.Substring(0, fileNameBFirstDotIndex);

      int prefixCompareResult = CompareAlphabetically(prefixFileNameA, prefixFileNameB);

      // Prefixes are equal --> compare suffix
      if (prefixCompareResult.Equals(0))
      {
        string suffixFileNameA = fileNameAWhithoutExtension.Substring(fileNameAFirstDotIndex + 1);
        string suffixFileNameB = fileNameBWhithoutExtension.Substring(fileNameBFirstDotIndex + 1);

        // Suffix is numeric
        if (
          int.TryParse(
            suffixFileNameA,
            NumberStyles.Integer | NumberStyles.Number,
            NumberFormatInfo.InvariantInfo,
            out int numberA)
          && int.TryParse(
            suffixFileNameB,
            NumberStyles.Integer | NumberStyles.Number,
            NumberFormatInfo.InvariantInfo,
            out int numberB))
        {
          return numberA.CompareTo(numberB);
        }

        return CompareAlphabetically(treeElementA.ElementInfo.Name, treeElementB.ElementInfo.Name);
      }

      return prefixCompareResult;
    }

    private int CompareAlphabetically(string treeElementA, string treeElementB)
    {
      char punctuationA = char.IsPunctuation(treeElementA, 0) ? treeElementA[0] : ' ';
      char punctuationB = char.IsPunctuation(treeElementB, 0) ? treeElementA[0] : ' ';
      if (!char.IsWhiteSpace(punctuationA) && !char.IsWhiteSpace(punctuationB))
      {
        return punctuationA.CompareTo(punctuationB);
      }
      if (char.IsWhiteSpace(punctuationA) && char.IsWhiteSpace(punctuationB))
      {
        return String.Compare(treeElementA, treeElementB, StringComparison.OrdinalIgnoreCase);
      }

      return char.IsWhiteSpace(punctuationB) ? -1 : 1;
    }

    protected virtual void OnExpandedChanged(bool newValue, bool oldValue)
    {
      this.ExpandedChanged?.Invoke(this, new ValueChangedEventArgs<bool>(newValue, oldValue));
    }

    [NotifyPropertyChangedInvocator]
    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public event PropertyChangedEventHandler PropertyChanged;
    public event EventHandler<ValueChangedEventArgs<bool>> ExpandedChanged;
    private bool isSelected;
    private FileSystemTreeElement parentFileSystemTreeElement;
    private ObservableCollection<FileSystemTreeElement> childFileSystemTreeElements;

    private FileSystemInfo elementInfo;
    public FileSystemInfo ElementInfo
    {
      get => this.elementInfo;
      set
      {
        this.elementInfo = value; OnPropertyChanged();
        this.IsDirectory = this.ElementInfo is DirectoryInfo;
      }
    }

    private FileExtensions fileExtensionFilter;

    public FileExtensions FileExtensionFilter
    {
      get => this.fileExtensionFilter;
      set
      {
        if (value == this.fileExtensionFilter)
        {
          return;
        }

        this.fileExtensionFilter = value;
        OnPropertyChanged();
      }
    }

    public FileSystemTreeElement ParentFileSystemTreeElement
    {
      get => this.parentFileSystemTreeElement;
      set { this.parentFileSystemTreeElement = value; OnPropertyChanged(); }
    }

    public FileSystemTreeElement RootFileSystemTreeElement
    {
      get => this.parentFileSystemTreeElement;
      set { this.parentFileSystemTreeElement = value; OnPropertyChanged(); }
    }

    public ObservableCollection<FileSystemTreeElement> ChildFileSystemTreeElements
    {
      get => this.childFileSystemTreeElements;
      set
      {
        this.childFileSystemTreeElements = value;
        OnPropertyChanged();
      }
    }

    private bool isExpanded;
    public bool IsExpanded
    {
      get => this.isExpanded;
      set
      {
        bool oldValue = this.isExpanded;
        this.isExpanded = value;
        OnPropertyChanged();
        OnExpandedChanged(this.isExpanded, oldValue);
      }
    }

    private bool isEmptyElement;
    public bool IsEmptyElement
    {
      get => this.isEmptyElement;
      private set => this.isEmptyElement = value;
    }

    private bool isHidden;
    public bool IsHidden
    {
      get { return this.isHidden; }
      set
      {
        this.isHidden = value;
        OnPropertyChanged();
      }
    }

    private bool isSystem;
    public bool IsSystem
    {
      get { return this.isSystem; }
      set
      {
        this.isSystem = value;
        OnPropertyChanged();
      }
    }

    public bool IsSelected
    {
      get => this.isSelected;
      set
      {
        this.isSelected = value;
        OnPropertyChanged();
      }
    }

    private bool isVisible;
    public bool IsVisible
    {
      get => this.isVisible;
      set
      {
        this.isVisible = value;
        OnPropertyChanged();
      }
    }

    private object id;
    public object Id
    {
      get => this.id;
      set
      {
        this.id = value;
        OnPropertyChanged();
      }
    }

    private int fileSystemTreePreLoadDepth;
    public int FileSystemTreePreLoadDepth
    {
      get { return this.fileSystemTreePreLoadDepth; }
      set
      {
        this.fileSystemTreePreLoadDepth = value;
        OnPropertyChanged();
      }
    }

    private bool hasLazyChildren;
    public bool HasLazyChildren
    {
      get { return this.hasLazyChildren; }
      set
      {
        this.hasLazyChildren = value;
        OnPropertyChanged();
      }
    }

    private bool isLazyLoading;
    public bool IsLazyLoading
    {
      get { return this.isLazyLoading; }
      set
      {
        this.isLazyLoading = value;
        OnPropertyChanged();
      }
    }

    private bool isArchive;
    public bool IsArchive
    {
      get { return this.isArchive; }
      set
      {
        this.isArchive = value;
        OnPropertyChanged();
      }
    }

    private bool isDirectory;   
    public bool IsDirectory
    {
      get { return this.isDirectory; }
      private set 
      { 
        this.isDirectory = value; 
        OnPropertyChanged();
      }
    }

    public bool IsExistingDirectory => this.IsDirectory && Directory.Exists(this.ElementInfo.FullName);

    private bool isSystemDirectory;   
    public bool IsSystemDirectory
    {
      get { return this.isSystemDirectory; }
      set 
      { 
        this.isSystemDirectory = value; 
        OnPropertyChanged();
      }
    }

    private string alternativeIElementName;   
    public string AlternativeIElementName
    {
      get { return this.alternativeIElementName; }
      set 
      { 
        this.alternativeIElementName = value; 
        OnPropertyChanged();
      }
    }

    /// <inheritdoc />
    public int CompareTo(FileSystemTreeElement other)
    {
      if (object.ReferenceEquals(this, other))
      {
        return 0;
      }

      if (object.ReferenceEquals(null, other))
      {
        return 1;
      }

      return FileSystemTreeSortComparison(this, other);
    }

    #region Implementation of IComparer<in FileSystemTreeElement>

    public int Compare(FileSystemTreeElement x, FileSystemTreeElement y)
    {
      return x == null ? 1 : y == null ? -1 : x.CompareTo(y);
    }

    #endregion

    #region Implementation of IComparer

    public int Compare(object x, object y)
    {
      return Compare(x as FileSystemTreeElement, y as FileSystemTreeElement);
    }

    #endregion
  }
}
