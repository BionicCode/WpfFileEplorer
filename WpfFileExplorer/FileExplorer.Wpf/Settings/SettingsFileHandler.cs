﻿using System;
using FileExplorer.Wpf.Resources;

namespace FileExplorer.Wpf.Settings
{
  public class SettingsFileHandler
  {
    protected SettingsFileHandler()
    {
      this.FileExplorerSettingsWriter = new FileExplorerSettingsWriter(FileExplorerSettingsResources.SectionGroupName);
      this.FileExplorerSettingsReader = new FileExplorerSettingsReader(FileExplorerSettingsResources.RecentFileSectionName, FileExplorerSettingsResources.SectionGroupName);
      this.GeneralSettingsWriter = new GeneralAppSettingsWriter(GlobalSettingsResources.SectionGroupName);
      this.GeneralSettingsReader = new GeneralAppSettingsReader(GlobalSettingsResources.SectionName, GlobalSettingsResources.SectionGroupName);
    }
    
    private static readonly object SyncLock = new object();
    private static SettingsFileHandler _Instance;
    public static SettingsFileHandler Instance
    {
      get
      {
        if (SettingsFileHandler._Instance == null)
        {
          lock (SettingsFileHandler.SyncLock)
          {
            if (SettingsFileHandler._Instance == null)
            {
              try
              {
                //                SettingsFileHandler._Instance = Activator.CreateInstance(typeof(SettingsFileHandler).DeclaringType ?? typeof(SettingsFileHandler), BindingFlags.Instance | BindingFlags.CreateInstance | BindingFlags.NonPublic, Type.DefaultBinder, null, CultureInfo.CurrentCulture) as SettingsFileHandler;
                SettingsFileHandler._Instance = new SettingsFileHandler();
              }
              catch (MissingMethodException missingMethodException)
              {
                missingMethodException.Data.Add(Resources.Resources.ExceptionDataKey, "No non-public constructor found");
                throw;
              }
            }
          }
        }
        return SettingsFileHandler._Instance;
      }
    }
    
    public IFileExplorerSettingsWriter FileExplorerSettingsWriter { get; set; }
    public IFileExplorerSettingsReader FileExplorerSettingsReader { get; set; }
    public IGeneralAppSettingsWriter GeneralSettingsWriter { get; set; }
    public IGeneralAppSettingsReader GeneralSettingsReader { get; set; }
  }
}
