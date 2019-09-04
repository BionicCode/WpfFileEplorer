namespace FileExplorer.Wpf
{
  public interface INullObject<out TObject>
  {
    bool IsNull { get; }
    TObject NullObject { get; }
  }
}
