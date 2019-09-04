﻿using System;
using System.ComponentModel;

namespace FileExplorer.Wpf.Settings.View
{
    public interface ISettingsPageData : INotifyPropertyChanged
    {
      Guid Guid { get; }
      string Title { get; set; }
      string Description { get; set; }
      string DisplaySettingKey { get; set; }
      bool AffectsPerformance { get; set; }
    }
}
