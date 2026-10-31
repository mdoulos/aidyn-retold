namespace AidynEnhanced.Core.Ui;

/// <summary>How the runtime's UI screen (<c>UiScreen</c>) scales the original's 320 × 240 screen up to the window.</summary>
public enum UiFilter
{
    /// <summary>
    /// Sharp: drawn at the window's resolution, each image the screen draws at its own size magnified pixel for pixel and
    /// each it scales filtered there, so text and pictures are as crisp as their pixels allow, and a larger replacement shows
    /// its detail. The default.
    /// </summary>
    Pixels,

    /// <summary>
    /// As the console's video output shows it: each line's 320 pixels spread over 640, with the
    /// pixels between them the average of their neighbours, then scaled up sharp. The emulator's
    /// captures of the original show exactly this.
    /// </summary>
    VideoOut,

    /// <summary>Bilinear in both directions.</summary>
    Smooth,
}
