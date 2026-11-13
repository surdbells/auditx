using System.Globalization;
using AuditX.Domain.Reports;

namespace AuditX.Infrastructure.Reports;

/// <summary>An sRGB colour with the handful of operations the report renderers need.</summary>
public readonly record struct Rgb(byte R, byte G, byte B)
{
    public static readonly Rgb White = new(255, 255, 255);
    public static readonly Rgb Black = new(0, 0, 0);

    /// <summary><c>#rrggbb</c>.</summary>
    public string Hex => $"#{R:x2}{G:x2}{B:x2}";

    /// <summary><c>RRGGBB</c> (no hash, upper-case) — the form OpenXML colour attributes take.</summary>
    public string HexNoHash => $"{R:X2}{G:X2}{B:X2}";

    /// <summary>Parse <c>#rrggbb</c>; anything else yields <paramref name="fallback"/>.</summary>
    public static Rgb Parse(string? hex, Rgb fallback)
    {
        if (hex is { Length: 7 } && hex[0] == '#'
            && byte.TryParse(hex.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
            && byte.TryParse(hex.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
            && byte.TryParse(hex.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return new Rgb(r, g, b);
        }

        return fallback;
    }

    /// <summary>Blend toward <paramref name="other"/>: 0 = this colour, 1 = the other.</summary>
    public Rgb Mix(Rgb other, double amount)
    {
        var t = Math.Clamp(amount, 0, 1);
        return new Rgb(Lerp(R, other.R, t), Lerp(G, other.G, t), Lerp(B, other.B, t));
    }

    /// <summary>WCAG relative luminance (0 = black, 1 = white).</summary>
    public double Luminance => (0.2126 * Channel(R)) + (0.7152 * Channel(G)) + (0.0722 * Channel(B));

    /// <summary>WCAG contrast ratio against <paramref name="other"/> (1..21).</summary>
    public double ContrastWith(Rgb other)
    {
        var (lighter, darker) = Luminance >= other.Luminance ? (Luminance, other.Luminance) : (other.Luminance, Luminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static byte Lerp(byte from, byte to, double t) => (byte)Math.Round(from + ((to - from) * t));

    private static double Channel(byte value)
    {
        var c = value / 255d;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}

/// <summary>
/// The colours a report is drawn with, derived from the institution's two brand colours (M8). The raw brand colours
/// are kept for decoration (rules, bands); text and fills use derived shades so the report stays legible whatever
/// colour the institution picked — a pale yellow brand must not produce pale-yellow headings on white paper.
/// </summary>
public sealed class BrandPalette
{
    /// <summary>Minimum contrast for heading text on white (WCAG AA for normal text).</summary>
    private const double MinTextContrast = 4.5;

    public BrandPalette(ReportBranding branding)
    {
        Primary = Rgb.Parse(branding.PrimaryColor, Rgb.Parse(ReportBranding.DefaultPrimaryColor, Rgb.Black));
        Accent = Rgb.Parse(branding.AccentColor, Rgb.Parse(ReportBranding.DefaultAccentColor, Rgb.Black));
        HeadingText = ReadableOn(Primary, Rgb.White);
        TableHeaderFill = Primary.Mix(Rgb.White, 0.88);
        // Measured against the tint it sits on, not against white — a pale brand makes the two differ.
        TableHeaderText = ReadableOn(Primary.Mix(Rgb.Black, 0.35), TableHeaderFill);
    }

    /// <summary>The primary brand colour as configured — for the header band and rules.</summary>
    public Rgb Primary { get; }

    /// <summary>The accent brand colour as configured — for section rules.</summary>
    public Rgb Accent { get; }

    /// <summary>The primary colour, darkened only as far as needed to read as heading text on white.</summary>
    public Rgb HeadingText { get; }

    /// <summary>A pale tint of the primary colour for table header rows.</summary>
    public Rgb TableHeaderFill { get; }

    /// <summary>A dark shade of the primary colour that reads on <see cref="TableHeaderFill"/>.</summary>
    public Rgb TableHeaderText { get; }

    /// <summary>Darken <paramref name="colour"/> only as far as needed to read as text on <paramref name="background"/>.</summary>
    private static Rgb ReadableOn(Rgb colour, Rgb background)
    {
        var candidate = colour;
        for (var step = 0; step < 30 && candidate.ContrastWith(background) < MinTextContrast; step++)
        {
            candidate = candidate.Mix(Rgb.Black, 0.12);
        }

        return candidate;
    }
}

/// <summary>Reads the pixel size of a PNG or JPEG without decoding it — enough to place a logo at its aspect ratio.</summary>
public static class ImageHeader
{
    public enum Kind
    {
        Unknown,
        Png,
        Jpeg,
    }

    public static bool TryRead(ReadOnlySpan<byte> bytes, out Kind kind, out int width, out int height)
    {
        kind = Kind.Unknown;
        width = 0;
        height = 0;

        // PNG: 8-byte signature, then the IHDR chunk whose data starts at byte 16 (width, height — big-endian).
        if (bytes.Length >= 24 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
            height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
            kind = Kind.Png;
            return width > 0 && height > 0;
        }

        // JPEG: walk the segments to the first start-of-frame marker (SOF0..SOF15, minus the non-frame C4/C8/CC).
        if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xD8)
        {
            var i = 2;
            while (i + 9 < bytes.Length)
            {
                if (bytes[i] != 0xFF)
                {
                    i++;
                    continue;
                }

                var marker = bytes[i + 1];
                if (marker is 0xFF)
                {
                    i++;
                    continue;
                }

                if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
                {
                    height = (bytes[i + 5] << 8) | bytes[i + 6];
                    width = (bytes[i + 7] << 8) | bytes[i + 8];
                    kind = Kind.Jpeg;
                    return width > 0 && height > 0;
                }

                // Standalone markers carry no length; every other segment is skipped by its declared length.
                if (marker is 0xD8 or 0x01 or (>= 0xD0 and <= 0xD7))
                {
                    i += 2;
                    continue;
                }

                var length = (bytes[i + 2] << 8) | bytes[i + 3];
                if (length < 2)
                {
                    return false;
                }

                i += 2 + length;
            }
        }

        return false;
    }
}
