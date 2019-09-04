namespace FileExplorer.Wpf.Settings
{
  public interface IGeneralAppSettingsReader
  {
    bool TryReadValue(string key, out string value);
  }
}