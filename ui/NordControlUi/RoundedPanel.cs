using System.Drawing.Drawing2D;

namespace NordControlUi;

internal sealed class RoundedPanel : Panel
{
    public int CornerRadius { get; set; } = 18;

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateRegion();
    }

    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        var diameter = Math.Min(CornerRadius * 2, Math.Min(Width, Height));
        using var path = new GraphicsPath();
        path.AddArc(0, 0, diameter, diameter, 180, 90);
        path.AddArc(Width - diameter, 0, diameter, diameter, 270, 90);
        path.AddArc(Width - diameter, Height - diameter, diameter, diameter, 0, 90);
        path.AddArc(0, Height - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        var previousRegion = Region;
        Region = new Region(path);
        previousRegion?.Dispose();
    }
}
