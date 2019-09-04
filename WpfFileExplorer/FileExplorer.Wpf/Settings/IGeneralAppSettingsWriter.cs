namespace FileExplorer.Wpf.Settings
{
  public interface IGeneralAppSettingsWriter
  {
    void WriteEntry(string key, string value);
  }
}