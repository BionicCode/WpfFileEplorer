using System;
using System.Collections.ObjectModel;
using BionicLibrary.NetStandard.Generic;

namespace FileExplorer.Wpf.Settings.View.Generic
{
  public interface ISettingsItemsPageData<TValue> : ISettingsPageData<TValue>
  {
    event EventHandler<ValueChangedEventArgs<TValue>> ValuesChanged;
    ObservableCollection<TValue> DisplaySettingValues { get; set; }
  }
}