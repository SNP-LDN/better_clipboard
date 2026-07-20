using System.Windows;
using System.Windows.Media.Imaging;

namespace BetterClipboard;

public partial class ImagePreviewWindow : Window
{
    public ImagePreviewWindow(BitmapSource image)
    {
        InitializeComponent();
        PreviewImage.Source = image;
        ImageInfo.Text = $"{image.PixelWidth} × {image.PixelHeight}";
    }
}
