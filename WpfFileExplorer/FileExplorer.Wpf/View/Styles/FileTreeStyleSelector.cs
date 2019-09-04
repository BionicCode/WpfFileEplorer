using System.Windows;
using System.Windows.Controls;

namespace FileExplorer.Wpf.View.Styles
{
    public class FileTreeStyleSelector : StyleSelector
    {
      /// <inheritdoc />
      public override Style SelectStyle(object item, DependencyObject container)
      {
        if (item is FileSystemTreeElement directoryNode && directoryNode.IsArchive && container is ItemsControl itemContainer)
        {
          return itemContainer.TryFindResource("DirectoryTreeItemStyle") as Style;
        }
        return base.SelectStyle(item, container);
      }
    }
}
