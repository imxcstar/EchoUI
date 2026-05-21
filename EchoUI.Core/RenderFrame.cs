namespace EchoUI.Core;

public enum RenderBackendKind
{
    Cpu,
    Gpu
}

public sealed record RenderBackendCapabilities(
    bool RequiresFullFrame,
    bool SupportsPartialInvalidation,
    bool PresentsDirectlyToWindow,
    bool IsHardwareAccelerated,
    bool SupportsImageResources = true,
    bool SupportsNativeText = true,
    bool SupportsVectorText = true,
    bool SupportsAdvancedClipping = true);

public readonly record struct RenderTileId(int X, int Y);

public readonly record struct RenderTile(RenderTileId Id, LayoutBox Bounds, LayoutBox DirtyBounds, int Priority = 0);

public sealed record RenderFrame(
    int Width,
    int Height,
    IReadOnlyList<RenderCommand> Commands,
    IReadOnlyList<LayoutBox> DirtyRects,
    IReadOnlyList<RenderTile> DirtyTiles,
    int TileSize,
    long Version)
{
    public LayoutBox Viewport => new(0, 0, Width, Height);
}

public interface IRenderFrameBackend : IDisposable
{
    RenderBackendKind Kind { get; }

    RenderBackendCapabilities Capabilities { get; }

    void Submit(RenderFrame frame);
}

public static class TileGrid
{
    public const int DefaultTileSize = 256;

    public static IReadOnlyList<RenderTile> FromDirtyRects(int width, int height, IEnumerable<LayoutBox> dirtyRects, int tileSize = DefaultTileSize)
    {
        if (width <= 0 || height <= 0)
            return [];

        tileSize = Math.Max(1, tileSize);
        var viewport = new LayoutBox(0, 0, width, height);
        var tiles = new Dictionary<RenderTileId, RenderTile>();

        foreach (var dirty in dirtyRects)
        {
            var clippedDirty = Intersect(viewport, dirty);
            if (clippedDirty.Width <= 0 || clippedDirty.Height <= 0)
                continue;

            var left = Math.Max(0, (int)MathF.Floor(clippedDirty.X / tileSize));
            var top = Math.Max(0, (int)MathF.Floor(clippedDirty.Y / tileSize));
            var right = Math.Max(0, (int)MathF.Floor((clippedDirty.X + clippedDirty.Width - 1) / tileSize));
            var bottom = Math.Max(0, (int)MathF.Floor((clippedDirty.Y + clippedDirty.Height - 1) / tileSize));

            for (var y = top; y <= bottom; y++)
            {
                for (var x = left; x <= right; x++)
                {
                    var bounds = Intersect(viewport, new LayoutBox(x * tileSize, y * tileSize, tileSize, tileSize));
                    var tileDirty = Intersect(bounds, clippedDirty);
                    if (tileDirty.Width <= 0 || tileDirty.Height <= 0)
                        continue;

                    var id = new RenderTileId(x, y);
                    if (tiles.TryGetValue(id, out var existing))
                    {
                        tiles[id] = existing with
                        {
                            DirtyBounds = Union(existing.DirtyBounds, tileDirty),
                            Priority = Math.Max(existing.Priority, ResolvePriority(bounds, viewport))
                        };
                    }
                    else
                    {
                        tiles.Add(id, new RenderTile(id, bounds, tileDirty, ResolvePriority(bounds, viewport)));
                    }
                }
            }
        }

        return tiles.Values.OrderByDescending(t => t.Priority).ThenBy(t => t.Id.Y).ThenBy(t => t.Id.X).ToArray();
    }

    public static LayoutBox Intersect(LayoutBox a, LayoutBox b)
    {
        var left = Math.Max(a.X, b.X);
        var top = Math.Max(a.Y, b.Y);
        var right = Math.Min(a.X + a.Width, b.X + b.Width);
        var bottom = Math.Min(a.Y + a.Height, b.Y + b.Height);
        return new LayoutBox(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    public static LayoutBox Union(LayoutBox a, LayoutBox b)
    {
        if (a.Width <= 0 || a.Height <= 0)
            return b;
        if (b.Width <= 0 || b.Height <= 0)
            return a;

        var left = Math.Min(a.X, b.X);
        var top = Math.Min(a.Y, b.Y);
        var right = Math.Max(a.X + a.Width, b.X + b.Width);
        var bottom = Math.Max(a.Y + a.Height, b.Y + b.Height);
        return new LayoutBox(left, top, right - left, bottom - top);
    }

    private static int ResolvePriority(LayoutBox tile, LayoutBox viewport)
    {
        return Intersect(tile, viewport).Width > 0 ? 100 : 0;
    }
}
