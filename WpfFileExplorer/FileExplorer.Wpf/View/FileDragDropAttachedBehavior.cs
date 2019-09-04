using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using JetBrains.Annotations;

namespace FileExplorer.Wpf.View
{
  class FileDragDropAttachedBehavior
  {
    #region IsEnabled attached property

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
      "IsEnabled", typeof(bool), typeof(FileDragDropAttachedBehavior), new PropertyMetadata(false, FileDragDropAttachedBehavior.OnEnabledChanged));

    public static void SetIsEnabled([NotNull] DependencyObject attachingElement, Control value)
    {
      attachingElement.SetValue(FileDragDropAttachedBehavior.IsEnabledProperty, value);
    }

    public static Control GetIsEnabled([NotNull] DependencyObject attachingElement)
    {
      return (Control) attachingElement.GetValue(FileDragDropAttachedBehavior.IsEnabledProperty);
    }

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
      if ((bool) e.NewValue)
      {
        (d as FrameworkElement).Drop += FileDragDropAttachedBehavior.HandleDrop;
      }
      else
      {
        (d as FrameworkElement).Drop -= FileDragDropAttachedBehavior.HandleDrop;
      }
    }

    #endregion

    #region DragDropFileCollection attached property

    public static readonly DependencyProperty DragDropFileCollectionProperty = DependencyProperty.RegisterAttached(
      "DragDropFileCollection", typeof(ObservableCollection<string>), typeof(FileDragDropAttachedBehavior), new PropertyMetadata(new ObservableCollection<string>()));

    public static void SetDragDropFileCollection([NotNull] DependencyObject attachingElement, ObservableCollection<string> value)
    {
      attachingElement.SetValue(FileDragDropAttachedBehavior.DragDropFileCollectionProperty, value);
    }

    public static ObservableCollection<string> GetDragDropFileCollection([NotNull] DependencyObject attachingElement)
    {
      return attachingElement.GetValue(FileDragDropAttachedBehavior.DragDropFileCollectionProperty) as ObservableCollection<string>;
    }

    #endregion

    private static async void HandleDrop(object sender, DragEventArgs e)
    {
      if (sender is FileExplorer fileExplorer && e.Data.GetDataPresent(DataFormats.FileDrop, false))
      {
          string[] droppedFiles = e.Data.GetData(DataFormats.FileDrop, false) as string[] ?? new string[0];
          await fileExplorer.ExplorerViewModel.AddFilesAsync(droppedFiles.ToList(), true);
        FileDragDropAttachedBehavior.SetDragDropFileCollection(sender as DependencyObject,
          new ObservableCollection<string>(droppedFiles));
      }
    }
  }
}
