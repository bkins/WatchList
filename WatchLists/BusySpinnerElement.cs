using Microsoft.Maui;
using Microsoft.Maui.Graphics;

namespace WatchLists;

internal sealed class BusySpinnerElement : IWindowOverlayElement
{
    private float _rotation;

    public bool Contains(Point point)
    {
        return false;
    }

    public void Advance()
    {
        _rotation = (_rotation + 24f) % 360f;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var centerX = dirtyRect.Center.X;
        var centerY = dirtyRect.Center.Y;
        const float panelSize = 76f;
        const float spinnerSize = 38f;
        var panelX = centerX - panelSize / 2;
        var panelY = centerY - panelSize / 2;
        var spinnerX = centerX - spinnerSize / 2;
        var spinnerY = centerY - spinnerSize / 2;

        canvas.SaveState();
        canvas.FillColor = Color.FromArgb("#E61E1E1E");
        canvas.FillRoundedRectangle(panelX, panelY, panelSize, panelSize, 14f);
        canvas.StrokeColor = Color.FromArgb("#FFF3F4F6");
        canvas.StrokeSize = 4f;
        canvas.DrawArc(spinnerX
                     , spinnerY
                     , spinnerSize
                     , spinnerSize
                     , _rotation
                     , _rotation + 275f
                     , true
                     , false);
        canvas.RestoreState();
    }
}
