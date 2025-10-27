namespace DocProcessing.Application.Models.OCR;

/// <summary>
/// Represents a bounding box with normalized coordinates (0.0 to 1.0).
/// Coordinates are relative to the page dimensions.
/// </summary>
public sealed class BoundingBox
{
    /// <summary>
    /// X-coordinate of the top-left corner (normalized, 0.0 to 1.0).
    /// </summary>
    public double X { get; init; }

    /// <summary>
    /// Y-coordinate of the top-left corner (normalized, 0.0 to 1.0).
    /// </summary>
    public double Y { get; init; }

    /// <summary>
    /// Width of the bounding box (normalized, 0.0 to 1.0).
    /// </summary>
    public double Width { get; init; }

    /// <summary>
    /// Height of the bounding box (normalized, 0.0 to 1.0).
    /// </summary>
    public double Height { get; init; }

    /// <summary>
    /// Optional page number (1-based) if coordinates are page-specific.
    /// </summary>
    public int? PageNumber { get; init; }

    public BoundingBox(double x, double y, double width, double height, int? pageNumber = null)
    {
        if (x is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(x), "Must be between 0 and 1");
        if (y is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(y), "Must be between 0 and 1");
        if (width is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(width), "Must be between 0 and 1");
        if (height is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(height), "Must be between 0 and 1");
        if (pageNumber is < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Must be >= 1");

        X = x;
        Y = y;
        Width = width;
        Height = height;
        PageNumber = pageNumber;
    }
}
