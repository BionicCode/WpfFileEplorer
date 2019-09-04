using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace FileExplorer.Wpf
{
  class FileExplorerItemData : INotifyPropertyChanged
  {
    public FileExplorerItemData(string fileOrFolderPath)
    {
      this.FileName = fileOrFolderPath;
      if (File.Exists(fileOrFolderPath))
      {
        this.FileInfo = new FileInfo(fileOrFolderPath);
      }
      else if (Directory.Exists(fileOrFolderPath))
      {
        this.DirectoryInfo = new DirectoryInfo(fileOrFolderPath);
      }

      this.Children = new List<FileExplorerItemData>();
      this.Parent = this;
    }

    public bool TryGetFileInfo(out FileInfo fileInfo)
    {
      fileInfo = this.FileInfo;
      return fileInfo != null;
    }

    private DirectoryInfo directoryInfo;
    private FileExplorerItemData parent;
    private List<FileExplorerItemData> children;
    private string fileName;

    private FileInfo fileInfo;   
    public FileInfo FileInfo
    {
      get { return this.fileInfo; }
      set 
      { 
        this.fileInfo = value; 
        OnPropertyChanged();

        if (this.IsFile)
        {
          this.DirectoryInfo = null;
        }
      }
    }

    public DirectoryInfo DirectoryInfo
    {
      get { return this.directoryInfo; }
      set 
      { 
        this.directoryInfo = value; 
        OnPropertyChanged();

        if (this.IsDirectory)
        {
          this.FileInfo = null;
        }
      }
    }

    public bool IsDirectory => this.DirectoryInfo!= null;

    public bool IsFile => this.FileInfo != null;

    public FileExplorerItemData Parent { get { return this.parent; } set { this.parent = value; OnPropertyChanged(); } }

    public List<FileExplorerItemData> Children { get { return this.children; } set { this.children = value; OnPropertyChanged(); } }

    public string FileName { get { return this.fileName; } set { this.fileName = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler PropertyChanged;

    [NotifyPropertyChangedInvocator]
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
  }
}
