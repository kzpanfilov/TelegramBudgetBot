using SkiaSharp;

namespace TelegramBudgetBot.Services;

public static class ChartGenerator
{
    private static readonly SKColor BgColor = SKColor.Parse("#1F2A2E");
    private static readonly SKColor TextColor = SKColor.Parse("#E8EDEC");
    private static readonly SKColor AccentColor = SKColor.Parse("#4ECDC4");

    private static readonly SKColor[] BarColors =
    [
        SKColor.Parse("#4ECDC4"), SKColor.Parse("#FF6B6B"), SKColor.Parse("#FFE66D"),
        SKColor.Parse("#95E1D3"), SKColor.Parse("#F38181"), SKColor.Parse("#AA96DA"),
        SKColor.Parse("#FCBAD3"), SKColor.Parse("#A8D8EA"), SKColor.Parse("#C9B1FF")
    ];

    public static byte[] GenerateBarChart(Dictionary<string, decimal> data, string title)
    {
        var entries = data.OrderByDescending(kv => kv.Value).ToList();
        if (entries.Count == 0) return [];

        var total = entries.Sum(kv => kv.Value);
        var width = 800;
        var barHeight = 50;
        var padding = 60;
        var height = padding * 2 + entries.Count * (barHeight + 15) + 60;

        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;
        canvas.Clear(BgColor);

        using var titleFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 28);
        using var labelFont = new SKFont(SKTypeface.Default, 20);
        using var pctFont = new SKFont(SKTypeface.Default, 18);

        using var titlePaint = new SKPaint { Color = TextColor, IsAntialias = true };
        using var labelPaint = new SKPaint { Color = TextColor, IsAntialias = true };
        using var pctPaint = new SKPaint { Color = AccentColor, IsAntialias = true };

        canvas.DrawText(title, padding, padding, SKTextAlign.Left, titleFont, titlePaint);

        var maxVal = entries[0].Value;
        var barAreaWidth = width - padding * 2 - 150;
        var y = padding + 50;

        for (int i = 0; i < entries.Count; i++)
        {
            var (cat, amount) = entries[i];
            var pct = total > 0 ? amount / total : 0;
            var barWidth = maxVal > 0 ? (float)(amount / maxVal * barAreaWidth) : 0;
            var color = BarColors[i % BarColors.Length];

            using var barPaint = new SKPaint { Color = color, IsAntialias = true };
            var rect = new SKRect(padding, y, padding + barWidth, y + barHeight);
            canvas.DrawRoundRect(rect, 8, 8, barPaint);

            canvas.DrawText($"{cat}", padding, y - 5, SKTextAlign.Left, labelFont, labelPaint);
            canvas.DrawText($"{amount:N0}₽ ({pct:P0})", padding + barWidth + 10, y + 35, SKTextAlign.Left, pctFont, pctPaint);

            y += barHeight + 15;
        }

        canvas.DrawText($"Итого: {total:N0}₽", padding, y + 20, SKTextAlign.Left, titleFont, titlePaint);

        using var image = surface.Snapshot();
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 90);
        return encoded.ToArray();
    }
}
