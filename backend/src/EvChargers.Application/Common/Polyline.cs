namespace EvChargers.Application.Common;

public static class Polyline
{
    /// <summary>Keeps at most <paramref name="max"/> evenly spaced points, always including the first and last.</summary>
    public static List<double[]> Downsample(IReadOnlyList<double[]> points, int max)
    {
        if (points.Count <= max) return [.. points];

        var step = (points.Count - 1) / (double)(max - 1);
        return Enumerable.Range(0, max)
            .Select(i => points[(int)Math.Round(i * step)])
            .ToList();
    }
}
