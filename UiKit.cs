using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ClipboardPDF;

enum Bg { None, Window, Surface, Canvas, Border }
enum Fg { None, Text, SubText, Accent }
enum ThemeMode { System = 0, Light = 1, Dark = 2 }

sealed class StyleInfo
{
    public Bg Bg;
    public Fg Fg;
    public float? FontPt;
    public FontStyle FontStyle;
}

/// <summary>
/// 配色 (ライト/ダーク) とフォントを一元管理する。
/// フォントは「pt × 実際のモニター DPI」から px 単位で作るので、どの倍率のモニターでも同じ見た目になる。
/// </summary>
static class Theme
{
    public static bool Dark { get; set; }

    static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);
    static Color Pick(Color light, Color dark) => Dark ? dark : light;

    public static Color Accent => Pick(C(0, 95, 184), C(0, 120, 212));          // ボタン背景用
    public static Color AccentHover => Pick(C(0, 84, 166), C(16, 132, 222));
    public static Color AccentDown => Pick(C(0, 74, 148), C(0, 102, 184));
    public static Color AccentText => Pick(C(0, 95, 184), C(96, 205, 255));     // 文字・アイコン用
    public static Color AccentLight => Pick(C(224, 238, 251), C(20, 52, 80));
    public static Color Window => Pick(C(243, 243, 243), C(32, 32, 32));
    public static Color Surface => Pick(Color.White, C(43, 43, 43));
    public static Color Canvas => Pick(C(249, 249, 250), C(28, 28, 28));
    public static Color Border => Pick(C(214, 214, 218), C(64, 64, 66));
    public static Color Text => Pick(C(27, 27, 31), C(240, 240, 242));
    public static Color SubText => Pick(C(96, 96, 104), C(170, 170, 178));
    public static Color Disabled => Pick(C(160, 160, 166), C(105, 105, 110));
    public static Color Hover => Pick(C(240, 240, 242), C(56, 56, 58));
    public static Color Down => Pick(C(228, 228, 232), C(66, 66, 68));
    public static Color Success => Pick(C(16, 124, 16), C(108, 203, 95));
    public static Color Info => Pick(C(0, 95, 184), C(96, 205, 255));
    public static Color Warning => Pick(C(196, 112, 0), C(255, 185, 70));
    public static Color Error => Pick(C(196, 43, 28), C(255, 153, 164));
    public static Color Placeholder => Pick(C(170, 176, 190), C(100, 100, 108));

    public static Color Of(Bg bg) => bg switch
    {
        Bg.Window => Window, Bg.Surface => Surface, Bg.Canvas => Canvas, Bg.Border => Border, _ => Color.Empty,
    };
    public static Color Of(Fg fg) => fg switch
    {
        Fg.Text => Text, Fg.SubText => SubText, Fg.Accent => AccentText, _ => Color.Empty,
    };

    /// <summary>Windows の「アプリ モード」がダークかどうか</summary>
    public static bool SystemPrefersDark()
    {
        try
        {
            var v = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1);
            return v is int i && i == 0;
        }
        catch { return false; }
    }

    public static bool Resolve(ThemeMode mode) => mode switch
    {
        ThemeMode.Dark => true, ThemeMode.Light => false, _ => SystemPrefersDark(),
    };

    // ---------- フォント ----------
    public const string UiFont = "Yu Gothic UI";
    static readonly Dictionary<(float, FontStyle, int), Font> fontCache = [];

    public static Font Font(float pt, FontStyle style, int dpi)
    {
        var key = (pt, style, dpi);
        if (!fontCache.TryGetValue(key, out var f))
            fontCache[key] = f = new Font(UiFont, pt * dpi / 72f, style, GraphicsUnit.Pixel);
        return f;
    }

    static string? iconFamily;
    /// <summary>Windows 11 は Segoe Fluent Icons、10 は Segoe MDL2 Assets</summary>
    public static string IconFamily => iconFamily ??= ResolveIconFamily();
    static string ResolveIconFamily()
    {
        using var fonts = new InstalledFontCollection();
        return fonts.Families.Any(f => f.Name == "Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
    }

    // ---------- スタイルの割り当てと適用 ----------
    static readonly ConditionalWeakTable<Control, StyleInfo> styles = new();

    public static T Style<T>(T c, Bg bg = Bg.None, Fg fg = Fg.None, float? pt = null, FontStyle fs = FontStyle.Regular) where T : Control
    {
        styles.AddOrUpdate(c, new StyleInfo { Bg = bg, Fg = fg, FontPt = pt, FontStyle = fs });
        return c;
    }

    /// <summary>root 以下に現在の配色と DPI に合ったフォントを適用</summary>
    public static void Apply(Control root, int dpi, float basePt)
    {
        root.Font = Font(basePt, FontStyle.Regular, dpi);
        ApplyTo(root, dpi, isRoot: true);
    }

    static void ApplyTo(Control c, int dpi, bool isRoot)
    {
        if (styles.TryGetValue(c, out var s))
        {
            if (s.Bg != Bg.None) c.BackColor = Of(s.Bg);
            if (s.Fg != Fg.None) c.ForeColor = Of(s.Fg);
            if (s.FontPt is float pt) c.Font = Font(pt, s.FontStyle, dpi);
            else if (!isRoot) c.ResetFont();
        }
        else if (!isRoot) c.ResetFont();

        switch (c)
        {
            case Segmented seg: seg.Restyle(); break;
            case TextBox tb: tb.BackColor = Theme.Surface; tb.ForeColor = Theme.Text; break;
        }
        c.Invalidate();
        foreach (Control child in c.Controls) ApplyTo(child, dpi, false);
    }
}

/// <summary>アイコンフォントのコードポイント</summary>
static class Glyph
{
    public const string History = "";
    public const string Paste = "";
    public const string Folder = "";
    public const string Up = "";
    public const string Down = "";
    public const string Rotate = "";
    public const string Delete = "";
    public const string Clear = "";
    public const string Pdf = "";
    public const string Check = "";
    public const string Info = "";
    public const string Warning = "";
    public const string Error = "";
    public const string Close = "";
    public const string Photo = "";
    public const string Theme = "";
    public const string Settings = "";
    public const string Browse = "";
}

static class Icons
{
    /// <summary>アイコンフォントの 1 文字をビットマップに描画 (DPI に合わせたサイズで)</summary>
    public static Bitmap Render(string glyph, Color color, int dpi, int logicalSize = 16, bool mirror = false)
    {
        int px = (int)Math.Round(logicalSize * dpi / 96.0);
        var bmp = new Bitmap(px, px);
        using (var g = Graphics.FromImage(bmp))
        using (var font = new Font(Theme.IconFamily, px * 0.82f, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(color))
        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.DrawString(glyph, font, brush, new RectangleF(0, 0, px, px), sf);
        }
        if (mirror) bmp.RotateFlip(RotateFlipType.RotateNoneFlipX);
        return bmp;
    }

    static readonly Dictionary<(string, int, int, int, bool), Bitmap> cache = [];
    /// <summary>描画のたびに作り直さないよう、色・DPI ごとにキャッシュ</summary>
    public static Bitmap Cached(string glyph, Color color, int dpi, int logicalSize, bool mirror)
    {
        var key = (glyph, color.ToArgb(), dpi, logicalSize, mirror);
        if (!cache.TryGetValue(key, out var bmp))
            cache[key] = bmp = Render(glyph, color, dpi, logicalSize, mirror);
        return bmp;
    }
}

enum ButtonKind { Primary, Standard, Subtle, Link }

record ButtonInfo(ButtonKind Kind, string? Glyph, bool Mirror, int IconSize = 16);

/// <summary>
/// 自前で描画するボタン。標準の Flat ボタンは無効時の文字色を OS が黒っぽい灰色で描いてしまい
/// ダークモードで読めなくなるため、背景・枠・アイコン・文字をすべてテーマの色で描く。
/// </summary>
sealed class ThemedButton : Button
{
    public ButtonInfo Info { get; }
    bool hover, down;

    public ThemedButton(ButtonInfo info)
    {
        Info = info;
        Tag = info;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
    }

    int IconPx => Info.Glyph == null ? 0 : (int)Math.Round(Info.IconSize * DeviceDpi / 96.0);
    int Gap => Info.Glyph != null && Text.Length > 0 ? LogicalToDeviceUnits(6) : 0;
    const TextFormatFlags TextFlags = TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;

    public override Size GetPreferredSize(Size proposedSize)
    {
        var t = Text.Length > 0 ? TextRenderer.MeasureText(Text, Font, Size.Empty, TextFlags) : Size.Empty;
        int contentH = Math.Max(IconPx, Text.Length > 0 ? Font.Height : 0);
        var size = new Size(Padding.Horizontal + IconPx + Gap + t.Width + 2, Padding.Vertical + contentH + 2);
        return new Size(Math.Max(size.Width, MinimumSize.Width), Math.Max(size.Height, MinimumSize.Height));
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { hover = down = false; Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var parentBg = Parent?.BackColor ?? Theme.Surface;
        bool en = Enabled, hot = en && hover, press = en && down;
        Color bg, border, fore, glyph;
        switch (Info.Kind)
        {
            case ButtonKind.Primary:
                bg = !en ? Theme.Hover : press ? Theme.AccentDown : hot ? Theme.AccentHover : Theme.Accent;
                border = bg;
                fore = glyph = en ? Color.White : Theme.Disabled;
                break;
            case ButtonKind.Standard:
                bg = press ? Theme.Down : hot ? Theme.Hover : Theme.Surface;
                border = Theme.Border;
                fore = en ? Theme.Text : Theme.Disabled;
                glyph = en ? Theme.AccentText : Theme.Disabled;
                break;
            case ButtonKind.Link:
                bg = hot || press ? Theme.AccentLight : parentBg;
                border = Color.Empty;
                fore = glyph = en ? Theme.AccentText : Theme.Disabled;
                break;
            default:
                bg = press ? Theme.Down : hot ? Theme.Hover : parentBg;
                border = Color.Empty;
                fore = glyph = en ? Theme.Text : Theme.Disabled;
                break;
        }

        g.Clear(bg);
        if (border != Color.Empty)
        {
            using var p = new Pen(border);
            g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }

        int icon = IconPx, gap = Gap;
        int textW = Text.Length > 0 ? TextRenderer.MeasureText(g, Text, Font, Size.Empty, TextFlags).Width : 0;
        int x = (Width - (icon + gap + textW)) / 2;
        if (Info.Glyph != null)
        {
            var bmp = Icons.Cached(Info.Glyph, glyph, DeviceDpi, Info.IconSize, Info.Mirror);
            g.DrawImage(bmp, x, (Height - bmp.Height) / 2, bmp.Width, bmp.Height);
        }
        if (textW > 0)
            TextRenderer.DrawText(g, Text, Font, new Rectangle(x + icon + gap, 0, textW + 2, Height), fore, bg, TextFlags);

        if (Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -3, -3), fore, bg);
    }
}

static class Ui
{
    /// <summary>テキスト量に応じて大きさが自動で決まるボタン (固定サイズにしない = 高 DPI でも欠けない)</summary>
    public static Button Button(string text, string? glyph, ButtonKind kind, EventHandler onClick,
        string? tooltip = null, ToolTip? tip = null, bool mirror = false, int iconSize = 16)
    {
        var b = new ThemedButton(new ButtonInfo(kind, glyph, mirror, iconSize))
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            // 種類が違っても縦の余白は同じにして高さを揃える
            Padding = kind switch
            {
                ButtonKind.Primary => new Padding(16, 6, 18, 6),
                ButtonKind.Subtle => new Padding(8, 5, 10, 5),
                ButtonKind.Link => new Padding(6, 3, 6, 3),
                _ => new Padding(12, 6, 14, 6),
            },
            Margin = kind is ButtonKind.Subtle or ButtonKind.Link ? new Padding(0, 0, 2, 0) : new Padding(0, 0, 8, 0),
        };
        if (kind is ButtonKind.Primary or ButtonKind.Link)
            Theme.Style(b, pt: kind == ButtonKind.Link ? 11f : 10f, fs: FontStyle.Bold);
        b.Click += onClick;
        if (tooltip != null && tip != null) tip.SetToolTip(b, tooltip);
        return b;
    }

    /// <summary>並んだコントロールの高さを、いちばん高いものに揃える</summary>
    public static void EqualizeHeights(IEnumerable<Control> controls)
    {
        var list = controls.ToList();
        foreach (var c in list) c.MinimumSize = Size.Empty;
        int h = list.Max(c => c.GetPreferredSize(Size.Empty).Height);
        foreach (var c in list) c.MinimumSize = new Size(0, h);
    }

    public static Label Label(string text, Fg fg = Fg.Text, float? pt = null, FontStyle fs = FontStyle.Regular, Padding? margin = null) =>
        Theme.Style(new Label
        {
            Text = text,
            AutoSize = true,
            Margin = margin ?? new Padding(0, 0, 8, 0),
            Anchor = AnchorStyles.Left,
            TextAlign = ContentAlignment.MiddleLeft,
        }, fg: fg, pt: pt, fs: fs);

    /// <summary>ダークモードでも読めるツールチップ</summary>
    public static ToolTip ToolTip()
    {
        var tip = new ToolTip { InitialDelay = 400, OwnerDraw = true };
        tip.Draw += (_, e) =>
        {
            using (var bg = new SolidBrush(Theme.Surface)) e.Graphics.FillRectangle(bg, e.Bounds);
            using (var p = new Pen(Theme.Border)) e.Graphics.DrawRectangle(p, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
            TextRenderer.DrawText(e.Graphics, e.ToolTipText, e.Font, e.Bounds, Theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };
        return tip;
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    static extern int SetWindowTheme(IntPtr hwnd, string? app, string? idList);
    /// <summary>ListView などのスクロールバー・選択色をテーマに合わせる</summary>
    public static void NativeTheme(Control c) => SetWindowTheme(c.Handle, Theme.Dark ? "DarkMode_Explorer" : "Explorer", null);

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    /// <summary>Windows 11 の角丸ウィンドウ (10 では無視される)</summary>
    public static void RoundCorners(IntPtr hwnd)
    {
        int pref = 2; // DWMWCP_ROUND
        try { DwmSetWindowAttribute(hwnd, 33, ref pref, sizeof(int)); } catch { }
    }
    /// <summary>タイトルバーをダーク/ライトに切り替え</summary>
    public static void DarkTitleBar(IntPtr hwnd, bool dark)
    {
        int v = dark ? 1 : 0;
        try { DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int)); } catch { }
    }
}

/// <summary>Windows 11 風のトグルスイッチ</summary>
sealed class ToggleSwitch : CheckBox
{
    public ToggleSwitch()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AutoSize = true;
        Cursor = Cursors.Hand;
        Padding = new Padding(0);
    }

    int TrackH => (int)(Font.Height * 0.95);
    int TrackW => TrackH * 2;
    int Gap => Font.Height / 2;

    public override Size GetPreferredSize(Size proposedSize)
    {
        var t = TextRenderer.MeasureText(Text, Font);
        return new Size(TrackW + Gap + t.Width + 2, Math.Max(TrackH, t.Height) + 4);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int y = (Height - TrackH) / 2;
        var track = new RectangleF(1, y, TrackW - 2, TrackH);
        using (var path = Pill(track))
        {
            if (Checked)
            {
                using var b = new SolidBrush(Enabled ? Theme.Accent : Theme.Disabled);
                g.FillPath(b, path);
            }
            else
            {
                using var p = new Pen(Theme.SubText, Math.Max(1f, TrackH / 14f));
                g.DrawPath(p, path);
            }
        }
        float d = TrackH * (Checked ? 0.62f : 0.5f);
        float cx = Checked ? track.Right - TrackH / 2f : track.Left + TrackH / 2f;
        using (var knob = new SolidBrush(Checked ? Color.White : Theme.SubText))
            g.FillEllipse(knob, cx - d / 2, y + (TrackH - d) / 2, d, d);

        var textRect = new Rectangle(TrackW + Gap, 0, Width - TrackW - Gap, Height);
        TextRenderer.DrawText(g, Text, Font, textRect, Enabled ? Theme.Text : Theme.Disabled,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, ClientRectangle);
    }

    static GraphicsPath Pill(RectangleF r)
    {
        var p = new GraphicsPath();
        float d = r.Height;
        p.AddArc(r.X, r.Y, d, d, 90, 180);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 180);
        p.CloseFigure();
        return p;
    }
}

/// <summary>セグメント (択一) ボタン。選択で太字にしない = 大きさが変わらない</summary>
sealed class Segmented : FlowLayoutPanel
{
    readonly List<RadioButton> buttons = [];
    public IReadOnlyList<RadioButton> Buttons => buttons;
    public event EventHandler? SelectedIndexChanged;

    public Segmented(ToolTip tip, params (string text, string tooltip)[] items)
    {
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        WrapContents = false;
        Margin = new Padding(0, 0, 16, 0);
        Padding = new Padding(0);
        Anchor = AnchorStyles.Left;
        Theme.Style(this, bg: Bg.Surface);
        foreach (var (text, tt) in items)
        {
            var rb = new RadioButton
            {
                Text = text,
                Appearance = Appearance.Button,
                FlatStyle = FlatStyle.Flat,
                AutoSize = true,
                Padding = new Padding(12, 5, 12, 5),
                Margin = new Padding(0, 0, -1, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
            };
            rb.CheckedChanged += (_, _) =>
            {
                StyleOne(rb);
                if (rb.Checked) SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            };
            tip.SetToolTip(rb, tt);
            buttons.Add(rb);
            Controls.Add(rb);
        }
    }

    public void Restyle() { foreach (var b in buttons) StyleOne(b); }

    static void StyleOne(RadioButton rb)
    {
        rb.BackColor = rb.Checked ? Theme.AccentLight : Theme.Surface;
        rb.ForeColor = rb.Checked ? Theme.AccentText : Theme.Text;
        rb.FlatAppearance.BorderColor = Theme.Border;
        rb.FlatAppearance.CheckedBackColor = Theme.AccentLight;
        rb.FlatAppearance.MouseOverBackColor = rb.Checked ? Theme.AccentLight : Theme.Hover;
        rb.FlatAppearance.MouseDownBackColor = Theme.Down;
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => buttons.FindIndex(b => b.Checked);
        set { if (value >= 0 && value < buttons.Count) buttons[value].Checked = true; }
    }
}

/// <summary>ライト/ダークに合わせたドロップダウン メニュー</summary>
sealed class ThemedMenu : ContextMenuStrip
{
    sealed class Colors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Theme.Surface;
        public override Color ImageMarginGradientBegin => Theme.Surface;
        public override Color ImageMarginGradientMiddle => Theme.Surface;
        public override Color ImageMarginGradientEnd => Theme.Surface;
        public override Color MenuBorder => Theme.Border;
        public override Color MenuItemBorder => Theme.Hover;
        public override Color MenuItemSelected => Theme.Hover;
        public override Color CheckBackground => Theme.AccentLight;
        public override Color CheckSelectedBackground => Theme.AccentLight;
        public override Color CheckPressedBackground => Theme.AccentLight;
        public override Color SeparatorDark => Theme.Border;
        public override Color SeparatorLight => Theme.Surface;
    }

    sealed class MenuRenderer : ToolStripProfessionalRenderer
    {
        public MenuRenderer() : base(new Colors()) { RoundedEdges = false; }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Tag as string == "sub" ? Theme.SubText : Theme.Text;
            base.OnRenderItemText(e);
        }
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var g = e.Graphics;
            var r = e.ImageRectangle;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var p = new Pen(Theme.AccentText, Math.Max(1.5f, r.Height / 8f));
            g.DrawLines(p, [new PointF(r.Left + r.Width * .2f, r.Top + r.Height * .55f),
                            new PointF(r.Left + r.Width * .42f, r.Top + r.Height * .75f),
                            new PointF(r.Left + r.Width * .8f, r.Top + r.Height * .3f)]);
        }
    }

    public ThemedMenu()
    {
        Renderer = new MenuRenderer();
        ShowImageMargin = true;
        ShowCheckMargin = false;
    }

    public void ApplyTheme(int dpi)
    {
        Font = Theme.Font(10f, FontStyle.Regular, dpi);
        BackColor = Theme.Surface;
        ForeColor = Theme.Text;
        Renderer = new MenuRenderer();
    }
}

/// <summary>一覧が空のときに表示する案内</summary>
sealed class EmptyState : Panel
{
    public EmptyState()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        Theme.Style(this, bg: Bg.Canvas);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        int dpi = DeviceDpi;
        float scale = dpi / 96f;
        int iconPx = (int)(56 * scale);

        var title = Theme.Font(13f, FontStyle.Bold, dpi);
        var body = Theme.Font(10f, FontStyle.Regular, dpi);
        string t = L.T("まだ画像がありません", "No images yet");
        string b = L.T(
            "画像をコピーすると、自動でここに追加されます。\n" +
            "［クリップボード履歴から取り込み］で Win+V の履歴をまとめて読み込めます。\n" +
            "Ctrl+V で貼り付け、または画像ファイルをここにドロップしても追加できます。",
            "Copy an image and it is added here automatically.\n" +
            "Use [Import clipboard history] to load everything in your Win+V history at once.\n" +
            "You can also paste with Ctrl+V or drop image files here.");
        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak;
        int w = Math.Min(Width - (int)(32 * scale), (int)(600 * scale));
        var tSize = TextRenderer.MeasureText(g, t, title, new Size(w, 0), flags);
        var bSize = TextRenderer.MeasureText(g, b, body, new Size(w, 0), flags);
        int gap = (int)(12 * scale);
        int total = iconPx + gap + tSize.Height + gap / 2 + bSize.Height;
        int y = Math.Max(gap, (Height - total) / 2);
        int x = (Width - w) / 2;

        using (var icon = Icons.Render(Glyph.Photo, Theme.Placeholder, dpi, 56))
            g.DrawImage(icon, (Width - icon.Width) / 2, y);
        y += iconPx + gap;
        TextRenderer.DrawText(g, t, title, new Rectangle(x, y, w, tSize.Height), Theme.Text, flags);
        y += tSize.Height + gap / 2;
        TextRenderer.DrawText(g, b, body, new Rectangle(x, y, w, bSize.Height), Theme.SubText, flags);
    }
}
