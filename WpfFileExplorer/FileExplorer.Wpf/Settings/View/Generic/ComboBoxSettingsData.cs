using System;
using System.Collections.ObjectModel;
using BionicLibrary.NetStandard.Generic;

namespace FileExplorer.Wpf.Settings.View.Generic
{
  public class ComboBoxSettingsData<TValue> : SettingsItemsPageData<TValue>, IComboBoxSettingsData
  {
    private EventHandler<ValueChangedEventArgs<object>> valueChanged;
    private readonly object syncLock = new object();

    #region Implementation of ISettingsPageData<object>

    event EventHandler<ValueChangedEventArgs<object>> ISettingsPageData<Object>.ValueChanged
    {
      add {
        lock (this.syncLock)
        {
          this.valueChanged += value;
        }
      }
      remove
      {
        EventHandler<ValueChangedEventArgs<object>> eventHandler = this.valueChanged;
        if (eventHandler != null)
        {
          lock (this.syncLock)
          {
            if (eventHandler != null)
            {
              eventHandler -= value;
            }
          }
        }
      }
    }
  
    object ISettingsPageData<object>.DisplaySettingValue
    {
      get => this.DisplaySettingValue;
      set
      {
        if (value is TValue newValue)
        {
          TValue oldValue = this.DisplaySettingValue;
          this.DisplaySettingValue = newValue;
          OnPropertyChanged();
          OnValueChanged(newValue, oldValue);
        }
      }
    }
  
    object ISettingsPageData<object>.DefaultDisplaySettingValue
    {
      get => this.DefaultDisplaySettingValue;
      set
      {
        if (value is TValue newValue)
        {
          TValue oldValue = this.DefaultDisplaySettingValue;
          this.DefaultDisplaySettingValue = newValue;
          OnPropertyChanged();
          OnValueChanged(newValue, oldValue);
        }
      }
    }

    #endregion

    #region Implementation of ISettingsItemsPageData<object>


    private ObservableCollection<object> displaySettingValues;
    
    ObservableCollection<object> ISettingsItemsPageData<object>.DisplaySettingValues
    {
      get => this.displaySettingValues;
      set
      {
        object oldValue = this.displaySettingValues;
        this.displaySettingValues = value;

        //OnValuesChanged(this.displaySettingValues, oldValue);
        OnPropertyChanged();
      }
    }

    #endregion
  }
}
