using System.Windows;
using System.Windows.Controls;

namespace FileExplorer.Wpf.View.Templates
{
  public class FileExplorerTemplateSelector : DataTemplateSelector
  {
    #region Overrides of DataTemplateSelector

    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
      if (!(container is FrameworkElement itemContainer))
      {
        return base.SelectTemplate(item, container);
      }

      if (item is FileSystemTreeElement)
      {
        return itemContainer.TryFindResource("DirectoryTreeItemTemplate") as DataTemplate;
      }

      //if (item is FileInfo)
      //{
      //  return itemContainer.TryFindResource("FileItemTemplate") as DataTemplate;
      //}

      return base.SelectTemplate(item, container);

    }

    #endregion
    }
}
