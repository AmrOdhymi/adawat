using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Adawat.Core;
using Adawat.Core.Images;
using ImageMagick;

namespace Adawat.App;

public static class QrScanner
{
    /// <summary>
    /// Launches screen snipping to select an area containing a QR code,
    /// decodes it in the background, copies the result to clipboard, and notifies the user.
    /// </summary>
    public static void StartScan()
    {
        ScreenCapture.StartSnipping(bitmap =>
        {
            Task.Run(() =>
            {
                try
                {
                    using var stream = new MemoryStream();
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    encoder.Save(stream);
                    stream.Position = 0;

                    using var image = new MagickImage(stream);
                    var codes = Recognition.Codes(image);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (codes.Count == 0)
                        {
                            Hud.Shared.ShowMessage(L10n.T("No QR codes found"), Hud.Style.Failure);
                            return;
                        }

                        var text = string.Join(Environment.NewLine, codes);
                        Adawat.Core.Clipboard.CopyText(text);
                        Sounds.Tick();
                        Hud.Shared.ShowMessage(L10n.T("QR Code copied to clipboard"), Hud.Style.Success);
                        QrResultWindow.ShowResult(text);
                    });
                }
                catch (Exception ex)
                {
                    Log.Write($"QR scan error: {ex}");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Hud.Shared.ShowMessage(L10n.T("Failed to read QR code"), Hud.Style.Failure);
                    });
                }
            });
        }, L10n.T("Select QR code on screen"));
    }
}
