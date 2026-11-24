namespace ClipboardPDF;

enum ToastKind { Success, Info, Warning, Error }

record ToastAction(string Label, Action Run);

/// <summary>メインウィンドウ下部中央にトーストを積み上げて表示する</summary>
sealed class ToastHost
{
    const float FontPt = 11.5f;
    const int MaxVisible = 3;
    readonly Form owner;
    readonly Func<int> bottomInset;
    readonly List<ToastForm> toasts = [];

    public ToastHost(Form owner, Func<int> bottomInset)
    {
        this.owner = owner;
        this.bottomInset = bottomInset;
        owner.Move += (_, _) => Layout();
        owner.Resize += (_, _) => Layout();
        owner.FormClosing += (_, _) => CloseAll();
        // 別 DPI のモニターへ移ったら古い倍率のトーストは閉じる
        owner.DpiChanged += (_, _) => CloseAll();
    }

    /// <summary>
    /// すべて閉じる。ウィンドウメッセージ処理中 (DPI 変更など) に直接 Close すると
    /// 破棄済みフォームへのアクセスで落ちるため、必ず後回しにして閉じる。
    /// </summary>
    public void CloseAll()
    {
        foreach (var t in toasts.ToList()) SafeClose(t);
    }

    void SafeClose(ToastForm t)
    {
        toasts.Remove(t);
        if (t.IsDisposed) return;
        t.Hide();
        if (owner.IsHandleCreated && !owner.IsDisposed)
            owner.BeginInvoke(() => { if (!t.IsDisposed) t.Close(); });
        else
            t.Dispose();
    }

    public void Show(string message, ToastKind kind, params ToastAction[] actions) => ShowKeyed(null, message, kind, actions);

    /// <summary>同じ key のトーストが表示中なら、新しく積まずに文言だけ更新する (連続コピー時など)</summary>
    public void ShowKeyed(string? key, string message, ToastKind kind, params ToastAction[] actions)
    {
        if (owner.IsDisposed || !owner.IsHandleCreated) return;
        toasts.RemoveAll(t => t.IsDisposed);

        if (key != null && toasts.FirstOrDefault(t => t.Key == key && !t.IsClosing) is { } existing)
        {
            existing.UpdateMessage(message);
            Layout();
            return;
        }
        foreach (var old in toasts.Where(t => t.Message == message).ToList()) SafeClose(old);
        while (toasts.Count >= MaxVisible) SafeClose(toasts[0]);

        var toast = new ToastForm(message, kind, actions, owner.DeviceDpi, FontPt) { Key = key };
        toast.FormClosed += (_, _) => { toasts.Remove(toast); Layout(); };
        toast.SizeChanged += (_, _) => Layout();
        toast.Expired += (_, _) => SafeClose(toast);
        toasts.Add(toast);
        Layout();
        toast.Show(owner);
        Layout();
    }

    void Layout()
    {
        if (owner.IsDisposed || owner.WindowState == FormWindowState.Minimized) return;
        var client = owner.RectangleToScreen(owner.ClientRectangle);
        int gap = owner.LogicalToDeviceUnits(8);
        int y = client.Bottom - bottomInset() - gap;
        for (int i = toasts.Count - 1; i >= 0; i--)
        {
            var t = toasts[i];
            if (t.IsDisposed) continue;
            var size = t.Visible ? t.Size : t.PreferredSize;
            y -= size.Height;
            t.Location = new Point(client.Left + (client.Width - size.Width) / 2, y);
            y -= gap;
        }
    }
}

sealed class ToastForm : Form
{
    readonly System.Windows.Forms.Timer timer = new() { Interval = 15 };
    readonly Color accent;
    readonly int lifetimeMs;
    readonly Label text;
    int elapsed;
    bool closing;

    public string Message { get; private set; }
    public string? Key { get; init; }
    public bool IsClosing => closing;
    /// <summary>表示時間が過ぎてフェードアウトし終えた</summary>
    public event EventHandler? Expired;

    public ToastForm(string message, ToastKind kind, ToastAction[] actions, int dpi, float fontPt)
    {
        Message = message;
        this.fontPt = fontPt;
        this.glyph = kind switch
        {
            ToastKind.Success => Glyph.Check,
            ToastKind.Warning => Glyph.Warning,
            ToastKind.Error => Glyph.Error,
            _ => Glyph.Info,
        };
        SuspendLayout();
        // WinForms の自動スケーリングは使わない。トーストは最初にメイン画面 (高 DPI) の倍率で作られ、
        // 100% のモニターへ移ると自動で縮小されて、既に 100% 用にしたフォントがさらに小さくなるため。
        // 倍率は ApplyDpi で自前で合わせる。
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(18, 12, 10, 12);
        Opacity = 0;
        Theme.Style(this, Bg.Surface, Fg.Text);

        accent = kind switch
        {
            ToastKind.Success => Theme.Success,
            ToastKind.Warning => Theme.Warning,
            ToastKind.Error => Theme.Error,
            _ => Theme.Info,
        };
        lifetimeMs = kind == ToastKind.Error ? 7000 : actions.Length > 0 ? 6500 : 3500;

        var row = Theme.Style(new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 4,
            RowCount = 1,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
        }, Bg.Surface);
        for (int i = 0; i < 4; i++) row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        icon = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.AutoSize,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 12, 0),
        };
        text = Theme.Style(new Label
        {
            Text = message,
            AutoSize = true,
            MaximumSize = new Size(520, 0),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 2, 14, 2),
        }, fg: Fg.Text);
        var actionPanel = Theme.Style(new FlowLayoutPanel
        {
            AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Left, Margin = new Padding(0),
        }, Bg.Surface);
        foreach (var a in actions)
        {
            var link = Ui.Button(a.Label, null, ButtonKind.Link, (_, _) => { a.Run(); BeginClose(); });
            Theme.Style(link, pt: fontPt, fs: FontStyle.Bold);
            actionPanel.Controls.Add(link);
        }
        var close = Ui.Button("", Glyph.Close, ButtonKind.Subtle, (_, _) => BeginClose(), iconSize: 14);
        close.Padding = new Padding(4);
        close.Margin = new Padding(6, 0, 0, 0);
        close.Anchor = AnchorStyles.Left;
        close.AccessibleName = L.T("閉じる", "Close");

        row.Controls.Add(icon, 0, 0);
        row.Controls.Add(text, 1, 0);
        row.Controls.Add(actionPanel, 2, 0);
        row.Controls.Add(close, 3, 0);
        Controls.Add(row);

        // 余白・最大幅は 96 DPI 基準の値を覚えておき、DPI ごとに換算する
        void Remember(Control c)
        {
            logical[c] = (c.Padding, c.Margin, c.MaximumSize);
            foreach (Control child in c.Controls) Remember(child);
        }
        Remember(this);

        ApplyDpi(dpi);
        ResumeLayout(false);
        PerformLayout();

        timer.Tick += OnTick;
    }

    readonly float fontPt;
    readonly string glyph;
    readonly PictureBox icon;
    readonly Dictionary<Control, (Padding Padding, Padding Margin, Size Max)> logical = [];

    void ApplyDpi(int dpi)
    {
        // ウィンドウ作成時に WinForms が「システム DPI → 実際の DPI」でフォントを勝手に縮めることがあるので、
        // DPI が同じでも毎回上書きする
        if (IsDisposed) return;
        SuspendLayout();
        static Padding S(Padding p, int dpi) => new(
            p.Left * dpi / 96, p.Top * dpi / 96, p.Right * dpi / 96, p.Bottom * dpi / 96);
        foreach (var (c, v) in logical)
        {
            c.Padding = S(v.Padding, dpi);
            c.Margin = S(v.Margin, dpi);
            if (v.Max != Size.Empty) c.MaximumSize = new Size(v.Max.Width * dpi / 96, v.Max.Height * dpi / 96);
        }
        var old = icon.Image;
        icon.Image = Icons.Render(glyph, accent, dpi, 22);
        old?.Dispose();
        Theme.Apply(this, dpi, fontPt);
        ResumeLayout(true);
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        // WinForms 側の縮小・拡大は止めて、自前で新しい DPI に合わせ直す (位置は ToastHost が決める)
        e.Cancel = true;
        base.OnDpiChanged(e);
        ApplyDpi(e.DeviceDpiNew);
    }

    public void UpdateMessage(string message)
    {
        if (IsDisposed) return;
        Message = message;
        text.Text = message;
        elapsed = 0;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000 /* WS_EX_NOACTIVATE */ | 0x00000080 /* WS_EX_TOOLWINDOW */;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Ui.RoundCorners(Handle);
        Ui.DarkTitleBar(Handle, Theme.Dark);
        ApplyDpi(DeviceDpi);
        timer.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        int bar = LogicalToDeviceUnits(4);
        using (var b = new SolidBrush(accent)) e.Graphics.FillRectangle(b, 0, 0, bar, Height);
        using var p = new Pen(Theme.Border);
        e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
    }

    void OnTick(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        if (closing)
        {
            Opacity = Math.Max(0, Opacity - 0.08);
            if (Opacity <= 0.01)
            {
                timer.Stop();
                Expired?.Invoke(this, EventArgs.Empty);
            }
            return;
        }
        if (Opacity < 1) { Opacity = Math.Min(1, Opacity + 0.1); return; }
        if (Bounds.Contains(Cursor.Position)) return; // マウスが乗っている間は消さない
        elapsed += timer.Interval;
        if (elapsed >= lifetimeMs) closing = true;
    }

    public void BeginClose() => closing = true;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Stop();
            timer.Dispose();
        }
        base.Dispose(disposing);
    }
}
