using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

if (args.Length < 2)
{
    Console.WriteLine("Usage: PixelAndBit.ImageTool <input.png> <output.png>");
    return 2;
}

var inputPath = args[0];
var outputPath = args[1];

using var image = Image.Load<Rgba32>(inputPath);

// Find dominant background colors (checkerboard) by sampling.
// We avoid the center to reduce logo influence.
var samples = new Dictionary<uint, int>(capacity: 1024);
void AddSample(Rgba32 p)
{
    // Quantize to reduce noise (5 bits per channel)
    var r = (uint)(p.R >> 3);
    var g = (uint)(p.G >> 3);
    var b = (uint)(p.B >> 3);
    var key = (r << 10) | (g << 5) | b;
    samples[key] = samples.TryGetValue(key, out var c) ? (c + 1) : 1;
}

image.ProcessPixelRows(accessor =>
{
    var w = accessor.Width;
    var h = accessor.Height;
    // sample stripes around edges
    for (var y = 0; y < h; y += Math.Max(1, h / 80))
    {
        var row = accessor.GetRowSpan(y);
        for (var x = 0; x < w; x += Math.Max(1, w / 80))
        {
            // edge bias
            if (x < w * 0.18 || x > w * 0.82 || y < h * 0.18 || y > h * 0.82)
                AddSample(row[x]);
        }
    }
});

var top = samples.OrderByDescending(kv => kv.Value).Take(4).Select(kv => kv.Key).ToArray();
Rgba32 KeyToColor(uint key)
{
    var r = (byte)(((key >> 10) & 31) << 3);
    var g = (byte)(((key >> 5) & 31) << 3);
    var b = (byte)((key & 31) << 3);
    return new Rgba32(r, g, b, 255);
}

var bg1 = top.Length > 0 ? KeyToColor(top[0]) : new Rgba32(240, 240, 240);
var bg2 = top.Length > 1 ? KeyToColor(top[1]) : new Rgba32(210, 210, 210);

Console.WriteLine($"BG1: {bg1.R},{bg1.G},{bg1.B}  BG2: {bg2.R},{bg2.G},{bg2.B}");

static float Dist(Rgba32 a, Rgba32 b)
{
    var dr = a.R - b.R;
    var dg = a.G - b.G;
    var db = a.B - b.B;
    return MathF.Sqrt(dr * dr + dg * dg + db * db);
}

static (float max, float min, float sat) SatMetrics(Rgba32 p)
{
    var r = p.R / 255f;
    var g = p.G / 255f;
    var b = p.B / 255f;
    var max = MathF.Max(r, MathF.Max(g, b));
    var min = MathF.Min(r, MathF.Min(g, b));
    var sat = max == 0 ? 0 : (max - min) / max;
    return (max, min, sat);
}

var changed = 0;
image.ProcessPixelRows(accessor =>
{
    for (var y = 0; y < accessor.Height; y++)
    {
        var row = accessor.GetRowSpan(y);
        for (var x = 0; x < row.Length; x++)
        {
            var p = row[x];

            // Detect checkerboard by closeness to the 2 dominant background colors.
            var (max, _, sat) = SatMetrics(p);
            var isNeutral = sat < 0.26f;

            if (isNeutral)
            {
                var d1 = Dist(p, bg1);
                var d2 = Dist(p, bg2);
                var d = MathF.Min(d1, d2);

                // Primary: kill background checker colors.
                if (d < 60f)
                {
                    row[x] = new Rgba32(p.R, p.G, p.B, 0);
                    changed++;
                    continue;
                }

                // Secondary: eradicate near-white matte/halo pixels (common on fake "transparent" PNG exports).
                // If it's bright + neutral, it cannot belong to the vivid logo — remove it.
                if (max > 0.86f)
                {
                    row[x] = new Rgba32(p.R, p.G, p.B, 0);
                    changed++;
                    continue;
                }

                // Tertiary: soften edge fringing where the exporter left a light matte.
                // Fade alpha down for neutral pixels that are "close-ish" to background.
                if (d < 95f && max > 0.58f)
                {
                    var t = (95f - d) / (95f - 60f); // 0..1
                    var newA = (byte)Math.Clamp(p.A - (int)(t * 170f), 0, 255);
                    if (newA != p.A)
                    {
                        row[x] = new Rgba32(p.R, p.G, p.B, newA);
                        changed++;
                    }
                }
            }
        }
    }
});

var corner = image[0, 0];
Console.WriteLine($"Corner after: A={corner.A} RGB={corner.R},{corner.G},{corner.B}");

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
image.Save(outputPath, new PngEncoder
{
    ColorType = PngColorType.RgbWithAlpha
});

Console.WriteLine($"Changed pixels: {changed:n0}");
Console.WriteLine($"Wrote: {outputPath}");
return 0;
