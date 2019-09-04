using System.Configuration;

namespace FileExplorer.Wpf.Settings.Data
{
    public class GeneralSettingsSection : ConfigurationSection
  {
    public GeneralSettingsSection()
    {
      //this.AutoOpenFiles = new 
    }

    public GeneralSettingsSection(NameValueConfigurationCollection entries)
    {
      base["entries"] = entries;
    }

    [ConfigurationProperty("entries", IsDefaultCollection = true, IsRequired = true)]
    [ConfigurationCollection(typeof(NameValueConfigurationCollection), AddItemName = "add", ClearItemsName = "clear", RemoveItemName = "remove")]
    public NameValueConfigurationCollection Entries => base["entries"] as NameValueConfigurationCollection;
  }
}
