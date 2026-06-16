using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MutsuPet.Services;

public static class ImageTransparencyService
{
    private const byte WhiteThreshold = 246;

    /// <summary>
    /// 读取角色图片并将与图片边缘相连的白色背景转为透明。
    /// </summary>
    public static ImageSource CreateTransparentImage(string imagePath)
    {
        var source = new BitmapImage();
        source.BeginInit();
        source.CacheOption = BitmapCacheOption.OnLoad;
        source.UriSource = new Uri(imagePath, UriKind.Absolute);
        source.EndInit();
        source.Freeze();

        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var width = converted.PixelWidth;
        var height = converted.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        converted.CopyPixels(pixels, stride, 0);

        var backgroundMask = FindEdgeBackgroundMask(pixels, width, height, stride);
        for (var index = 0; index < backgroundMask.Length; index++)
        {
            if (!backgroundMask[index])
            {
                continue;
            }

            var offset = index * 4;
            pixels[offset + 3] = 0;
        }

        var result = BitmapSource.Create(
            width,
            height,
            source.DpiX,
            source.DpiY,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        result.Freeze();
        return result;
    }

    /// <summary>
    /// 使用 flood fill 标记所有连接到图片边缘的白色背景像素。
    /// </summary>
    private static bool[] FindEdgeBackgroundMask(byte[] pixels, int width, int height, int stride)
    {
        var backgroundMask = new bool[width * height];
        var queuedMask = new bool[width * height];
        var queue = new Queue<int>();

        for (var x = 0; x < width; x++)
        {
            EnqueueIfBackground(pixels, width, height, stride, x, 0, queuedMask, queue);
            EnqueueIfBackground(pixels, width, height, stride, x, height - 1, queuedMask, queue);
        }

        for (var y = 0; y < height; y++)
        {
            EnqueueIfBackground(pixels, width, height, stride, 0, y, queuedMask, queue);
            EnqueueIfBackground(pixels, width, height, stride, width - 1, y, queuedMask, queue);
        }

        while (queue.Count > 0)
        {
            var index = queue.Dequeue();
            if (backgroundMask[index])
            {
                continue;
            }

            backgroundMask[index] = true;
            var x = index % width;
            var y = index / width;
            EnqueueIfBackground(pixels, width, height, stride, x - 1, y, queuedMask, queue);
            EnqueueIfBackground(pixels, width, height, stride, x + 1, y, queuedMask, queue);
            EnqueueIfBackground(pixels, width, height, stride, x, y - 1, queuedMask, queue);
            EnqueueIfBackground(pixels, width, height, stride, x, y + 1, queuedMask, queue);
        }

        return backgroundMask;
    }

    /// <summary>
    /// 若指定像素位于图内且接近白色背景，则加入 flood fill 队列。
    /// </summary>
    private static void EnqueueIfBackground(
        byte[] pixels,
        int width,
        int height,
        int stride,
        int x,
        int y,
        bool[] queuedMask,
        Queue<int> queue)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return;
        }

        var index = y * width + x;
        if (queuedMask[index] || !IsNearWhite(pixels, stride, x, y))
        {
            return;
        }

        queuedMask[index] = true;
        queue.Enqueue(index);
    }

    /// <summary>
    /// 判断 BGRA 像素是否属于可透明化的近白色背景。
    /// </summary>
    private static bool IsNearWhite(byte[] pixels, int stride, int x, int y)
    {
        var offset = y * stride + x * 4;
        var blue = pixels[offset];
        var green = pixels[offset + 1];
        var red = pixels[offset + 2];
        return red >= WhiteThreshold && green >= WhiteThreshold && blue >= WhiteThreshold;
    }
}
