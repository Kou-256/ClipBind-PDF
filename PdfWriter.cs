using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text;

namespace ClipboardPDF;

/// <summary>用紙サイズ (縦置きの幅・高さ, pt)</summary>
public sealed record Paper(string Name, string Detail, double W, double H)
{
    static double Mm(double mm) => mm * 72.0 / 25.4;

    public static readonly Paper[] All =
    [
        new("A3", "297 × 420 mm", Mm(297), Mm(420)),
        new("A4", "210 × 297 mm", Mm(210), Mm(297)),
        new("A5", "148 × 210 mm", Mm(148), Mm(210)),
        new("B4", "257 × 364 mm (JIS)", Mm(257), Mm(364)),
        new("B5", "182 × 257 mm (JIS)", Mm(182), Mm(257)),
        new("Letter", "8.5 × 11 in", 612, 792),
        new("Legal", "8.5 × 14 in", 612, 1008),
        new("Tabloid", "11 × 17 in", 792, 1224),
        new("はがき|Postcard", "100 × 148 mm", Mm(100), Mm(148)),
    ];

    public const int DefaultIndex = 1; // A4

    public string Label => Name.Contains('|') ? L.T(Name.Split('|')[0], Name.Split('|')[1]) : Name;
}

/// <summary>
/// 外部ライブラリなしで画像を JPEG (DCTDecode) として埋め込む最小限の PDF ライター。
/// </summary>
public static class PdfWriter
{
    const double Margin = 28.35; // 10mm

    /// <param name="paper">null なら各ページ = 画像サイズ。指定時は画像の向きに合わせて縦/横を選び、余白付きで中央配置</param>
    public static void Write(string path, IReadOnlyList<Image> images, Paper? paper, long jpegQuality)
    {
        if (images.Count == 0) throw new ArgumentException(L.T("画像がありません。", "No images."));

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        var offsets = new List<long>();
        int pageCount = images.Count;
        // オブジェクト番号: 1=Catalog, 2=Pages, ページ i ごとに (3+3i)=Page, (4+3i)=Contents, (5+3i)=Image
        int objCount = 2 + pageCount * 3;

        void W(string s) { var b = Encoding.ASCII.GetBytes(s); fs.Write(b, 0, b.Length); }
        void BeginObj(int n) { offsets.Add(fs.Position); W($"{n} 0 obj\n"); }

        W("%PDF-1.4\n");
        fs.Write([(byte)'%', 0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n']);

        BeginObj(1);
        W("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        BeginObj(2);
        var kids = string.Join(" ", Enumerable.Range(0, pageCount).Select(i => $"{3 + i * 3} 0 R"));
        W($"<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>\nendobj\n");

        for (int i = 0; i < pageCount; i++)
        {
            var img = images[i];
            byte[] jpeg = ToJpeg(img, jpegQuality);
            int pw = img.Width, ph = img.Height;

            // 画像の物理サイズ(pt): 96dpi 換算
            double iw = pw * 72.0 / 96.0, ih = ph * 72.0 / 96.0;
            double pageW, pageH, drawW, drawH, x, y;
            if (paper is null)
            {
                pageW = drawW = iw; pageH = drawH = ih; x = y = 0;
            }
            else
            {
                bool landscape = pw > ph;
                pageW = landscape ? paper.H : paper.W;
                pageH = landscape ? paper.W : paper.H;
                double m = Math.Min(Margin, Math.Min(pageW, pageH) * 0.05);
                double maxW = pageW - m * 2, maxH = pageH - m * 2;
                double scale = Math.Min(maxW / iw, maxH / ih);
                drawW = iw * scale; drawH = ih * scale;
                x = (pageW - drawW) / 2; y = (pageH - drawH) / 2;
            }

            int pageObj = 3 + i * 3, contentObj = pageObj + 1, imgObj = pageObj + 2;

            BeginObj(pageObj);
            W($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {F(pageW)} {F(pageH)}] " +
              $"/Resources << /XObject << /Im{i} {imgObj} 0 R >> >> /Contents {contentObj} 0 R >>\nendobj\n");

            var content = $"q\n{F(drawW)} 0 0 {F(drawH)} {F(x)} {F(y)} cm\n/Im{i} Do\nQ\n";
            BeginObj(contentObj);
            W($"<< /Length {content.Length} >>\nstream\n{content}\nendstream\nendobj\n");

            BeginObj(imgObj);
            W($"<< /Type /XObject /Subtype /Image /Width {pw} /Height {ph} /ColorSpace /DeviceRGB " +
              $"/BitsPerComponent 8 /Filter /DCTDecode /Length {jpeg.Length} >>\nstream\n");
            fs.Write(jpeg, 0, jpeg.Length);
            W("\nendstream\nendobj\n");
        }

        long xref = fs.Position;
        W($"xref\n0 {objCount + 1}\n0000000000 65535 f \n");
        foreach (var off in offsets) W($"{off:D10} 00000 n \n");
        W($"trailer\n<< /Size {objCount + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
    }

    static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    static byte[] ToJpeg(Image src, long quality)
    {
        // 透過部分を白で塗りつぶした 24bit RGB に変換してから JPEG 化
        using var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format24bppRgb);
        bmp.SetResolution(96, 96);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.White);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(src, 0, 0, src.Width, src.Height);
        }
        var codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        using var ep = new EncoderParameters(1);
        ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
        using var ms = new MemoryStream();
        bmp.Save(ms, codec, ep);
        return ms.ToArray();
    }
}
