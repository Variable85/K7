using ApexCharts;
using K7.Clients.Shared.Helpers;

namespace K7.Clients.Shared.UI.Helpers;

/// <summary>
/// Blazor-ApexCharts loads its module from a versioned URL (<c>...js?ver=x.y.z</c>). The GTK
/// (maui-labs) BlazorWebView handler does not strip query strings before looking the asset up
/// and answers 404, so the Linux desktop host points every chart at the plain module path.
/// Other hosts keep the library default.
/// </summary>
public static class ApexChartAssets
{
    public const string ModulePath = "./_content/Blazor-ApexCharts/js/blazor-apexcharts.js";

    public static ApexChartOptions<TItem> Prepare<TItem>(ApexChartOptions<TItem> options) where TItem : class
    {
        if (LinuxDesktopPlayback.IsLinuxDesktop)
            options.Blazor = new ApexChartsBlazorOptions { JavascriptPath = ModulePath };

        return options;
    }
}
