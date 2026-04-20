namespace CarolusNexus;

public enum ResponsiveBand
{
    Narrow,
    Medium,
    Wide
}

/// <summary>Shared width bands from the hosting control’s <c>Bounds.Width</c>.</summary>
public static class ResponsiveLayout
{
    public const double NarrowMax = 760;
    public const double MediumMax = 1100;

    public static ResponsiveBand GetBand(double width) =>
        width < NarrowMax ? ResponsiveBand.Narrow :
        width < MediumMax ? ResponsiveBand.Medium : ResponsiveBand.Wide;
}
