using FileExplorer.Wpf.Resources;
using FileExplorer.Wpf.Settings.Data;

namespace FileExplorer.Wpf.Settings
{
    public class GeneralAppSettingsReader : DefaultSettingsReader<GeneralSettingsSection>, IGeneralAppSettingsReader
  {
      public GeneralAppSettingsReader(string rootSectionName, string rootSectionGroupName) : base(rootSectionName, rootSectionGroupName)
      {
      }

      public bool TryReadValue(string key, out string value)
      {
        value = string.Empty;
        if (GetApplicationConfiguration().GetSectionGroup(GlobalSettingsResources.SectionGroupName)
                ?.Sections[GlobalSettingsResources.SectionName] is GeneralSettingsSection
              settingsSection && settingsSection.Entries.AllKeys.Contains(key))
        {
          value = settingsSection.Entries[key].Value;
          return true;
        }
        return false;
      }
    }
}
