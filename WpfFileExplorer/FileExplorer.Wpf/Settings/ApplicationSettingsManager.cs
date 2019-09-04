using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using BionicLibrary.Net.Utility;
using BionicLibrary.Net.Utility.Generic;
using BionicLibrary.NetStandard.Extensions;
using FileExplorer.Wpf.Resources;
using FileExplorer.Wpf.Settings.Data;
using FileExplorer.Wpf.Settings.View;
using JetBrains.Annotations;

namespace FileExplorer.Wpf.Settings
{
  public sealed class ApplicationSettingsManager : INotifyPropertyChanged
  {
    private const double DefaultWindowHeight = 700d;
    private const double DefaultWindowWidth = 1460d;
    private const string DefaultCustomFileExplorerExtensionFilter = "*";
    private const int MaxConcurrentDocumentsDefaultFallbackValue = 2;

    private ApplicationSettingsManager()
    {
      this.CommandLineApplicationPath = string.Empty;
      this.DefaultFilterIdValueCache = new Dictionary<string, bool>();
      this.IsAutoOpenSpecificFiles = true;
      this.SpecificAutoOpenFileNames = new ObservableCollection<string>();
      this.FileChunkSizeInLines = 1000;
      this.MaxNumberOfThreadsAllowed = Environment.ProcessorCount * 2;
      this.DeleteExtractedFilesOnAppClosingIsEnabled = true;
      this.HintTextIsEnabled = true;
      this.FileExplorerSettingsWriter = SettingsFileHandler.Instance.FileExplorerSettingsWriter;
      this.FileExplorerSettingsReader = SettingsFileHandler.Instance.FileExplorerSettingsReader;
      this.RecentFilesLimit = 30;
      this.RecentFiles = new ObservableCollection<FileInfo>();
      this.IsLiveSearchEnabled = true;
    }

    public void InitializeApplicationSettings()
    {
      InitializeFileExplorerSettings();
    }


    public void UpdateBinding(string propertyName)
    {
      OnPropertyChanged(propertyName);
    }

    private void InitializeFileExplorerSettings()
    {
      IEnumerable<FileElement> configElements = this.FileExplorerSettingsReader
        .ReadSection<FileExplorerRecentFilesSection>(FileExplorerSettingsResources.RecentFileSectionName)?.RecentFiles?
        .Cast<FileElement>();
      List<FileInfo> recentFileInfos = configElements?
        .Select(configElement => new FileInfo(configElement.FilePath))?
        .ToList();
      recentFileInfos?.RemoveAll(fileInfo => !fileInfo.Exists);
      this.RecentFiles = recentFileInfos == null
        ? new ObservableCollection<FileInfo>()
        : new ObservableCollection<FileInfo>(recentFileInfos);

      this.RecentFilesLimit = this.FileExplorerSettingsReader
        .ReadSection<FileExplorerRecentFilesSection>(FileExplorerSettingsResources.RecentFileSectionName)
        ?.MaxFiles ?? this.RecentFilesLimit;

      this.DefaultFilterIdValueCache = 
        this.FileExplorerSettingsReader.ReadSection<FileExplorerDefaultFilterSection>(FileExplorerSettingsResources.DefaultFiltersSectionName)
                                         ?.Filters
                                         .Cast<KeyValueConfigurationElement>()
                                         .ToDictionary(
                                           keyValueElement => keyValueElement.Key,
                                           keyValueElement =>
                                           {
                                             if (bool.TryParse(keyValueElement.Value, out bool value))
                                             {
                                               return value;
                                             }
                                             return true;
                                           }) 
                                       ?? new Dictionary<string, bool>();

      // Initialize on first start (empty settings)
      if (!this.DefaultFilterIdValueCache.Any())
      {
        this.DefaultFilterIdValueCache.Add(nameof(this.ExplorerIsShowingAnyFiles), true);
        this.DefaultFilterIdValueCache.Add(nameof(this.ExplorerIsShowingArchiveFiles), true);
        this.DefaultFilterIdValueCache.Add(nameof(this.ExplorerIsShowingLogFiles), true);
        this.DefaultFilterIdValueCache.Add(nameof(this.ExplorerIsShowingTxtFiles), true);
        this.DefaultFilterIdValueCache.Add(nameof(this.ExplorerIsShowingIniFiles), true);
      }

      this.DefaultFilterIdValueCache.Keys.ToList().ForEach(
        propertyName =>
        {
          switch (propertyName)
          {
            case nameof(this.ExplorerIsShowingArchiveFiles):
              this.ExplorerIsShowingArchiveFiles = this.DefaultFilterIdValueCache[propertyName];
              break;
            case nameof(this.ExplorerIsShowingAnyFiles):
              this.ExplorerIsShowingAnyFiles = this.DefaultFilterIdValueCache[propertyName];
              break;
            case nameof(this.ExplorerIsShowingIniFiles):
              this.ExplorerIsShowingIniFiles = this.DefaultFilterIdValueCache[propertyName];
              break;
            case nameof(this.ExplorerIsShowingTxtFiles):
              this.ExplorerIsShowingTxtFiles = this.DefaultFilterIdValueCache[propertyName];
              break;
            case nameof(this.ExplorerIsShowingLogFiles):
              this.ExplorerIsShowingLogFiles = this.DefaultFilterIdValueCache[propertyName];
              break;
          }
        });

      this.IsCustomFileFilteringEnabled = this.FileExplorerSettingsReader.ReadSection<FileExplorerCustomFilterSection>(FileExplorerSettingsResources.CustomFiltersSectionName)
                                         ?.IsCustomFilterEnabled ?? false;

      this.CustomExplorerFilterValue = this.FileExplorerSettingsReader.ReadSection<FileExplorerCustomFilterSection>(FileExplorerSettingsResources.CustomFiltersSectionName)
                                         ?.Filters;

      if (string.IsNullOrWhiteSpace(this.CustomExplorerFilterValue))
      {
        this.CustomExplorerFilterValue = ApplicationSettingsManager.DefaultCustomFileExplorerExtensionFilter;
      }

      this.DeleteExtractedFilesOnAppClosingIsEnabled = this.FileExplorerSettingsReader
        .ReadSection<FileExplorerRecentFilesSection>(FileExplorerSettingsResources.RecentFileSectionName)
        ?.IsDeleteTemporaryFilesEnabled ?? true;

      this.SpecificAutoOpenFileNames.AddRange(this.FileExplorerSettingsReader
        .ReadSection<StringValuesSection>(FileExplorerSettingsResources.AutoOpenFilesSectionName)
        ?.AutoOpenFiles?.Cast<ValueSettingsElement>()?.Select(settingsElement => settingsElement.Value) ?? new string[0]);

      this.AreFileExplorerSettingsInitialized = true;
      OnFileExplorerSettingsInitialized();
    }

    public void SavePendingChanges()
    {
      SaveFileExplorerSettings();
    }

    private void SaveFileExplorerSettings()
    {
      if (!Application.Current.Dispatcher.CheckAccess())
      {
        Application.Current.Dispatcher.InvokeAsync(SaveFileExplorerSettings);
        return;
      }

      if (this.FileExplorerSettingIsPending)
      {
        if (this.RecentFilesLimit > 0 && this.RecentFiles.Any())
        {
          SettingsFileHandler.Instance.FileExplorerSettingsWriter?.WriteRecentFilesData(
            this.RecentFiles.Distinct(EqualityComparer<FileInfo>.Default),
            this.RecentFilesLimit);
        }
        else
        {
          SettingsFileHandler.Instance.FileExplorerSettingsWriter?.ClearRecentFilesSection();
        }

        SettingsFileHandler.Instance.FileExplorerSettingsWriter?.WriteAreCustomFileFiltersEnabled(this.IsCustomFileFilteringEnabled);
        SettingsFileHandler.Instance.FileExplorerSettingsWriter?.WriteDefaultFileFilters(this.DefaultFilterIdValueCache);
        SettingsFileHandler.Instance.FileExplorerSettingsWriter?.WriteCustomFileFilters(this.CustomExplorerFilterValue);

        SettingsFileHandler.Instance.FileExplorerSettingsWriter?.WriteIsDeleteTemporaryFilesEnabled(this.DeleteExtractedFilesOnAppClosingIsEnabled);
        SettingsFileHandler.Instance.FileExplorerSettingsWriter?.WriteRecentFilesLimit(this.RecentFilesLimit);
        this.FileExplorerSettingsWriter.WriteAutoOpenFilesData(this.SpecificAutoOpenFileNames);

        this.FileExplorerSettingIsPending = false;
      }
    }

    private void SetFlagToNewStateOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
      this.IsAutoOpenSpecificFiles = this.SpecificAutoOpenFileNames.Any();
    }

    [NotifyPropertyChangedInvocator]
    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public event PropertyChangedEventHandler PropertyChanged;
    private static ApplicationSettingsManager _instance;
    private static readonly object _syncLock = new object();
    private bool FileExplorerSettingIsPending { get; set; }
    private IFileExplorerSettingsWriter FileExplorerSettingsWriter { get; set; }
    private IFileExplorerSettingsReader FileExplorerSettingsReader { get; set; }


    public ICommand ClearRecentFilesCommand =>
      new AsyncRelayCommand(this.RecentFiles.Clear, this.RecentFiles.Any);

    public static ApplicationSettingsManager Instance
    {
      get
      {
        if (ApplicationSettingsManager._instance == null)
        {
          lock (ApplicationSettingsManager._syncLock)
          {
            if (ApplicationSettingsManager._instance == null)
            {
              ApplicationSettingsManager._instance = new ApplicationSettingsManager();
            }
          }
        }

        return ApplicationSettingsManager._instance;
      }
      private set { ApplicationSettingsManager._instance = value; }
    }

    #region FileExplorer Settings

    private bool deleteExtractedFilesOnAppClosingIsEnabled;
    public bool DeleteExtractedFilesOnAppClosingIsEnabled
    {
      get { return this.deleteExtractedFilesOnAppClosingIsEnabled; }
      set
      {
        this.deleteExtractedFilesOnAppClosingIsEnabled = value;
        this.FileExplorerSettingIsPending = true;
        OnPropertyChanged();
      }
    }

    private bool hintTextIsEnabled;
    public bool HintTextIsEnabled
    {
      get { return this.hintTextIsEnabled; }
      set
      {
        this.hintTextIsEnabled = value;
        OnPropertyChanged();
      }
    }

    private bool isCustomFileFilteringEnabled;
    public bool IsCustomFileFilteringEnabled
    {
      get => this.isCustomFileFilteringEnabled;
      set
      {
        this.isCustomFileFilteringEnabled = value;
        this.FileExplorerSettingIsPending = true;
        OnPropertyChanged();
      }
    }

    private string customExplorerFilterValue;   
    public string CustomExplorerFilterValue
    {
      get { return this.customExplorerFilterValue; }
      set 
      { 
        this.customExplorerFilterValue = value; 
        this.FileExplorerSettingIsPending = true;
        OnPropertyChanged();
      }
    }

    private bool explorerIsShowingAnyFiles;
    public bool ExplorerIsShowingAnyFiles
    {
      get => this.explorerIsShowingAnyFiles;
      set
      {
        this.explorerIsShowingAnyFiles = value;
        this.FileExplorerSettingIsPending = true;
        OnPropertyChanged();
        this.DefaultFilterIdValueCache[nameof(this.ExplorerIsShowingAnyFiles)] = this.ExplorerIsShowingAnyFiles;
      }
    }

    private bool explorerIsShowingTxtFiles;
    public bool ExplorerIsShowingTxtFiles
    {
      get => this.explorerIsShowingTxtFiles;
      set
      {
        this.explorerIsShowingTxtFiles = value;
        this.FileExplorerSettingIsPending = true;
        OnPropertyChanged();
        this.DefaultFilterIdValueCache[nameof(this.ExplorerIsShowingTxtFiles)] = this.ExplorerIsShowingTxtFiles;
      }
    }

    private bool explorerIsShowingIniFiles;
    public bool ExplorerIsShowingIniFiles
    {
      get => this.explorerIsShowingIniFiles;
      set
      {
        this.explorerIsShowingIniFiles = value;
        this.FileExplorerSettingIsPending = true;
        OnPropertyChanged();
        this.DefaultFilterIdValueCache[nameof(this.ExplorerIsShowingIniFiles)] = this.ExplorerIsShowingIniFiles;
      }
    }

    private bool explorerIsShowingLogFiles;
    public bool ExplorerIsShowingLogFiles
    {
      get => this.explorerIsShowingLogFiles;
      set
      {
        this.explorerIsShowingLogFiles = value;
        this.FileExplorerSettingIsPending = true;
        OnPropertyChanged();
        this.DefaultFilterIdValueCache[nameof(this.ExplorerIsShowingLogFiles)] = this.ExplorerIsShowingLogFiles;
      }
    }

    private bool explorerIsShowingArchiveFiles;
    public bool ExplorerIsShowingArchiveFiles
    {
      get => this.explorerIsShowingArchiveFiles;
      set
      {
        this.explorerIsShowingArchiveFiles = value;
        this.FileExplorerSettingIsPending = true;
        OnPropertyChanged();
        this.DefaultFilterIdValueCache[nameof(this.ExplorerIsShowingArchiveFiles)] = this.ExplorerIsShowingArchiveFiles;
      }
    }

    private ObservableCollection<FileInfo> recentFiles;
    public ObservableCollection<FileInfo> RecentFiles
    {
      get { return this.recentFiles; }
      set
      {
        if (this.RecentFiles != null)
        {
          this.RecentFiles.CollectionChanged -= HandleRecentFileCollectionChanged;
        }

        this.recentFiles = value;
        if (this.RecentFiles != null)
        {
          this.RecentFiles.CollectionChanged += HandleRecentFileCollectionChanged;
        }

        this.FileExplorerSettingIsPending = true;
        OnPropertyChanged();
      }
    }

    public bool HasRecentFiles => this.RecentFiles?.Any() ?? false;

    private void HandleRecentFileCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
      List<FileInfo> distinctFileInfos = this.RecentFiles.Distinct().ToList();
      if (distinctFileInfos.Count() != this.RecentFiles.Count)
      {
        this.RecentFiles = new ObservableCollection<FileInfo>(distinctFileInfos);
      }

      this.FileExplorerSettingIsPending = true;
    }

    private int recentFilesLimit;
    public int RecentFilesLimit
    {
      get => this.recentFilesLimit;
      set
      {
        this.recentFilesLimit = value;
        this.FileExplorerSettingIsPending = true;
        OnPropertyChanged();
      }
    }

    private bool isAutoOpenSpecificFiles;
    public bool IsAutoOpenSpecificFiles
    {
      get => this.isAutoOpenSpecificFiles;
      set
      {
        this.isAutoOpenSpecificFiles = value;
        OnPropertyChanged();
      }
    }

    private ObservableCollection<string> specificAutoOpenFileNames;
    public ObservableCollection<string> SpecificAutoOpenFileNames
    {
      get => this.specificAutoOpenFileNames;
      set
      {
        if (this.SpecificAutoOpenFileNames != null)
        {
          this.SpecificAutoOpenFileNames.CollectionChanged -= SetFlagToNewStateOnCollectionChanged;
        }
        this.FileExplorerSettingIsPending = true;
        this.specificAutoOpenFileNames = value;
        this.SpecificAutoOpenFileNames.CollectionChanged += SetFlagToNewStateOnCollectionChanged;

        OnPropertyChanged();
      }
    }

    private Dictionary<string, bool> DefaultFilterIdValueCache { get; set; }

    #endregion

  

    private int fileChunkSizeInLines;
    public int FileChunkSizeInLines
    {
      get => this.fileChunkSizeInLines;
      set
      {
        this.fileChunkSizeInLines = value;
        OnPropertyChanged();
      }
    }

    private int maxNumberOfThreads;
    public int MaxNumberOfThreadsAllowed
    {
      get => this.maxNumberOfThreads;
      set
      {
        this.maxNumberOfThreads = value;
        OnPropertyChanged();
      }
    }

    #region general settings

    private int maxConcurrentOpeningDocuments;   
    public int MaxConcurrentOpeningDocuments
    {
      get { return this.maxConcurrentOpeningDocuments; }
      set 
      { 
        this.maxConcurrentOpeningDocuments = value; 
        OnPropertyChanged();
      }
    }

    private string commandLineApplicationPath;   
    public string CommandLineApplicationPath
    {
      get { return this.commandLineApplicationPath; }
      set 
      { 
        this.commandLineApplicationPath = value; 
        OnPropertyChanged();
      }
    }

    private string commandLineArguments;   
    public string CommandLineArguments
    {
      get { return this.commandLineArguments; }
      set 
      { 
        this.commandLineArguments = value; 
        OnPropertyChanged();
      }
    }

    private bool isMainToolBarAutoHideEnabled;
    public bool IsMainToolBarAutoHideEnabled
    {
      get => this.isMainToolBarAutoHideEnabled;
      set
      {
        this.isMainToolBarAutoHideEnabled = value;
        OnPropertyChanged();
      }
    }

    #endregion

    private bool isLiveSearchEnabled;   
    public bool IsLiveSearchEnabled
    {
      get => this.isLiveSearchEnabled;
      set 
      { 
        this.isLiveSearchEnabled = value; 
        OnPropertyChanged();
      }
    }

    public bool AreFileExplorerSettingsInitialized { get; private set; }
    public event EventHandler FileExplorerSettingsInitialized;

    private void OnFileExplorerSettingsInitialized()
    {
      this.FileExplorerSettingsInitialized?.Invoke(this, EventArgs.Empty);
    }
  }
}
