using System;
using BionicLibrary.NetStandard.Generic;

namespace FileExplorer.Wpf.Settings.View.Generic
{
  public interface ISettingsPageData<TValue> : ISettingsPageData
  {
    event EventHandler<ValueChangedEventArgs<TValue>> ValueChanged;
    TValue DisplaySettingValue { get; set; }
    TValue DefaultDisplaySettingValue { get; set; }
  }
}
