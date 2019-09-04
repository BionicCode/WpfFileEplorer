using System.ComponentModel;
using System.Windows.Media;
using Xceed.Wpf.Toolkit;

namespace FileExplorer.Wpf.Settings.View
{
  public interface IColorInfo : INullObject<IColorInfo>, INotifyPropertyChanged
  {
    Color Color { get; set; }
    ColorPicker ColorPicker { get; }
  }
}