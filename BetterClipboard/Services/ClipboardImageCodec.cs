using System.IO;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BetterClipboard.Services;

public static class ClipboardImageCodec
{
    public static EncodedImage EncodePng(BitmapSource source)
    {
        var normalized = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        normalized.Freeze();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(normalized));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        var bytes = stream.ToArray();
        return new EncodedImage(
            bytes,
            normalized.PixelWidth,
            normalized.PixelHeight,
            Convert.ToHexString(SHA256.HashData(bytes)));
    }

    public static EncodedImage? LoadImageFile(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            return decoder.Frames.Count == 0 ? null : EncodePng(decoder.Frames[0]);
        }
        catch
        {
            return null;
        }
    }

    public static BitmapSource? Decode(byte[] pngBytes, int decodePixelWidth = 0)
    {
        try
        {
            using var stream = new MemoryStream(pngBytes, writable: false);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            if (decodePixelWidth > 0)
            {
                image.DecodePixelWidth = decodePixelWidth;
            }

            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }
}

public sealed record EncodedImage(
    byte[] Bytes,
    int Width,
    int Height,
    string Hash);
