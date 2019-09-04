using System.Windows;

namespace FileExplorer.Wpf.Resources
{
  public static class ApplicationUiConstants
  {
    static ApplicationUiConstants()
    {
    }

    public static FontWeight ToolButtonFontWeight => FontWeights.Light;
    public static FontStretch ToolButtonFontStretch => FontStretches.UltraExpanded;
    public static double ToolButtonFontSize => 10;
    public static double DisabledControlOpacity => 0.4;
    public static double ProgressBarHeight => ApplicationUiConstants.OsIsWindows10 ? 2.0 : 6.0;

    public static double OsDependentMarginOffset => 3.0;

    private const double DEFAULT_GUTTER_LINE_LEFT_MARGIN = 37.0;

    private static Thickness _DefaultGutterLineMargin = new Thickness(ApplicationUiConstants.DEFAULT_GUTTER_LINE_LEFT_MARGIN, 0, 0, 0);
    public static Thickness DefaultGutterLineMargin
    {
      get { return ApplicationUiConstants._DefaultGutterLineMargin; }
      private set { ApplicationUiConstants._DefaultGutterLineMargin = value; }
    }

    private static Thickness _DefaultRegionItemMargin = new Thickness(15, 0, 0, 4);
    public static Thickness DefaultRegionItemMargin
    {
      get { return ApplicationUiConstants._DefaultRegionItemMargin; }
      private set { ApplicationUiConstants._DefaultRegionItemMargin = value; }
    }

    private static readonly CornerRadius _CornerRadius = new CornerRadius(3);
    public static CornerRadius CornerRadius => ApplicationUiConstants._CornerRadius;


    private static bool _osIsWindows10;
    public static bool OsIsWindows10
    {
      get { return ApplicationUiConstants._osIsWindows10; }
      set
      {
        ApplicationUiConstants._osIsWindows10 = value;

        //if (!ApplicationUiConstants.OsIsWindows10)
        //{
        //  ApplicationUiConstants.DefaultRegionItemMargin =
        //    new Thickness(ApplicationUiConstants.DefaultRegionItemMargin.Left,
        //      ApplicationUiConstants.DefaultRegionItemMargin.Top,
        //      ApplicationUiConstants.DefaultRegionItemMargin.Right,
        //      ApplicationUiConstants.DefaultRegionItemMargin.Bottom + ApplicationUiConstants.OsDependentMarginOffset);
        //}
      }
    }
  }
}
