using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DJCMS10.Models;

public static class TrackThumbnailGenerator
{
    public static ImageSource Generate(string title, int size = 40)
    {
        byte[] hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(title));

        var bitmap = new WriteableBitmap(
            size,
            size,
            96,
            96,
            PixelFormats.Bgra32,
            null);

        int stride = size * 4;
        byte[] pixels = new byte[size * stride];

        var color1 = Color.FromRgb(
            hash[0],
            hash[1],
            hash[2]);

        var color2 = Color.FromRgb(
            hash[3],
            hash[4],
            hash[5]);

        int gridSize = 5;
        int cellSize = size / gridSize;

        for (int x = 0; x < 3; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                bool filled = (hash[6 + x * gridSize + y] & 1) == 1;

                if (!filled)
                    continue;

                PaintCell(
                    pixels,
                    size,
                    x,
                    y,
                    cellSize,
                    color1);

                PaintCell(
                    pixels,
                    size,
                    gridSize - x - 1,
                    y,
                    cellSize,
                    color1);
            }
        }

        AddGradient(
            pixels,
            size,
            color2);

        bitmap.WritePixels(
            new Int32Rect(0, 0, size, size),
            pixels,
            stride,
            0);

        bitmap.Freeze();

        return bitmap;
    }

    private static void PaintCell(
        byte[] pixels,
        int imageSize,
        int gridX,
        int gridY,
        int cellSize,
        Color color)
    {
        int startX = gridX * cellSize;
        int startY = gridY * cellSize;

        for (int y = startY; y < startY + cellSize; y++)
        {
            for (int x = startX; x < startX + cellSize; x++)
            {
                int index = (y * imageSize + x) * 4;

                pixels[index + 0] = color.B;
                pixels[index + 1] = color.G;
                pixels[index + 2] = color.R;
                pixels[index + 3] = 255;
            }
        }
    }

    private static void AddGradient(
        byte[] pixels,
        int imageSize,
        Color overlay)
    {
        for (int y = 0; y < imageSize; y++)
        {
            double alpha = (double)y / imageSize * 0.35;

            for (int x = 0; x < imageSize; x++)
            {
                int index = (y * imageSize + x) * 4;

                pixels[index + 0] =
                    (byte)Math.Min(255,
                        pixels[index + 0] + overlay.B * alpha);

                pixels[index + 1] =
                    (byte)Math.Min(255,
                        pixels[index + 1] + overlay.G * alpha);

                pixels[index + 2] =
                    (byte)Math.Min(255,
                        pixels[index + 2] + overlay.R * alpha);
            }
        }
    }
}
