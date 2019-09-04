using System.Configuration;

namespace FileExplorer.Wpf.Settings
{
  public interface IFileExplorerSettingsReader
  {
    TSection ReadSection<TSection>(string sectionName) where TSection : ConfigurationSection, new();
  }
}