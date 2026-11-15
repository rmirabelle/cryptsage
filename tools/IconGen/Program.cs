using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IoPath = System.IO.Path;

namespace CryptSage.IconGen;

// Reads src/CryptSage.App/MountIcon.png (Microsoft Fluent UI Emoji "Computer disk" 3D,
// MIT licensed; source of truth is tools/IconGen/source-icon/computer_disk_3d.png)
// and produces AppIcon.ico alongside the app — used for the .exe icon, Window.Icon,
// and the custom WPF-UI TitleBar. MountIcon.png itself is used directly for in-app
// Image controls. Run: dotnet run --project tools/IconGen
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var appDir = args.Length > 0
            ? IoPath.GetFullPath(args[0])
            : IoPath.GetFullPath(IoPath.Combine(AppContext.BaseDirectory,
                "..", "..", "..", "..", "..", "src", "CryptSage.App"));

        var source = IoPath.Combine(appDir, "MountIcon.png");
        if (!File.Exists(source))
            throw new FileNotFoundException("Source PNG missing", source);

        var src = new BitmapImage();
        src.BeginInit();
        src.UriSource = new Uri(source);
        src.CacheOption = BitmapCacheOption.OnLoad;
        src.EndInit();
        src.Freeze();

        var sizes = new[] { 16, 24, 32, 48, 64, 96, 128, 192, 256 };
        var frames = sizes.Select(s => ScaleToPng(src, s)).ToList();

        var icoPath = IoPath.Combine(appDir, "AppIcon.ico");
        WriteIco(icoPath, sizes, frames);
        Console.WriteLine($"Wrote {icoPath}");
        return 0;
    }

    private static byte[] ScaleToPng(BitmapSource src, int size)
    {
        var scale = (double)size / Math.Max(src.PixelWidth, src.PixelHeight);
        var scaled = new TransformedBitmap(src, new ScaleTransform(scale, scale));
        RenderOptions.SetBitmapScalingMode(scaled, BitmapScalingMode.HighQuality);

        using var ms = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(scaled));
        encoder.Save(ms);
        return ms.ToArray();
    }

    private static void WriteIco(string path, int[] sizes, List<byte[]> frames)
    {
        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);

        bw.Write((ushort)0);
        bw.Write((ushort)1);
        bw.Write((ushort)frames.Count);

        var offset = 6 + frames.Count * 16;
        for (var i = 0; i < frames.Count; i++)
        {
            var dim = sizes[i] >= 256 ? (byte)0 : (byte)sizes[i];
            bw.Write(dim);
            bw.Write(dim);
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Write((ushort)1);
            bw.Write((ushort)32);
            bw.Write((uint)frames[i].Length);
            bw.Write((uint)offset);
            offset += frames[i].Length;
        }

        foreach (var f in frames) bw.Write(f);
    }
}
