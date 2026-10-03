using Microsoft.Maui.Platforms.Linux.Gtk4.Handlers;

namespace K7.Clients.MAUI.Platforms.Linux;

/// <summary>
/// ScrollView on GTK. The labs handler reports a 50px desired height whenever no
/// <c>HeightRequest</c> is set so the view never inflates its parent, which collapses every
/// auto-sized panel built around a ScrollView (settings and cast menus). Report the content
/// size instead, clamped to the constraint: parents cap it with <c>MaximumHeightRequest</c>
/// like on the other platforms.
/// </summary>
public sealed class LinuxScrollViewHandler : ScrollViewHandler
{
    public override Size GetDesiredSize(double widthConstraint, double heightConstraint)
    {
        if (VirtualView is ICrossPlatformLayout crossPlatform
            && VirtualView is View { HeightRequest: < 0 })
        {
            var measured = crossPlatform.CrossPlatformMeasure(widthConstraint, heightConstraint);
            var width = double.IsInfinity(widthConstraint) ? measured.Width : Math.Min(measured.Width, widthConstraint);
            var height = double.IsInfinity(heightConstraint) ? measured.Height : Math.Min(measured.Height, heightConstraint);
            return new Size(Math.Max(1, width), Math.Max(1, height));
        }

        return base.GetDesiredSize(widthConstraint, heightConstraint);
    }
}
