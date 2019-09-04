using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace FileExplorer.Wpf.Settings.Data
{
  public class StringValuesSection : ConfigurationSection
  {
    public StringValuesSection()
    {
    }

    public StringValuesSection(IEnumerable<string> stringConfigurationElements)
    {
      base["autoOpenFiles"] = new ValueSettingsElementCollection(stringConfigurationElements.Select((stringValue) => new ValueSettingsElement(stringValue)));
    }

    [ConfigurationProperty("autoOpenFiles", IsDefaultCollection = true)]
    [ConfigurationCollection(typeof(ValueSettingsElementCollection), AddItemName = "add", ClearItemsName = "clear", RemoveItemName = "remove")]
    public ValueSettingsElementCollection AutoOpenFiles
    {
      get => base["autoOpenFiles"] as ValueSettingsElementCollection;
      set => base["autoOpenFiles"] = value;
    }
  }
}
