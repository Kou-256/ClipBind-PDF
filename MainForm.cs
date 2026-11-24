using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32;
using WinClipboard = Windows.ApplicationModel.DataTransfer.Clipboard;
using StandardDataFormats = Windows.ApplicationModel.DataTransfer.StandardDataFormats;

namespace ClipboardPDF;

sealed class ImageEntry : IDisposable
{
    public required Image Image { get; init; }
    public required string Hash { get; init; }
    public string Key { get; } = Guid.NewGuid().ToString("N");
    public void Dispose() => Image.Dispose();
}

public sealed class MainForm : Form
{
    const int WM_CLIPBOARDUPDATE = 0x031D;
    const int LVM_SETICONSPACING = 0x1000 + 53;
    const float BaseFontPt = 10f;
    static readonly long[] QualityValues = [95, 85, 70];

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    readonly AppSettings settings = AppSettings.Load();
    readonly ToolTip tip = Ui.ToolTip();
    readonly ListView list = new();
    readonly ImageList thumbs = new() { ColorDepth = ColorDepth.Depth32Bit };
    readonly EmptyState empty = new() { Dock = DockStyle.Fill };
    readonly ToggleSwitch chkWatch = new() { Text = L.T("コピーした画像を自動で追加", "Auto-add copied images") };
    readonly Segmented segPage;
    readonly ThemedMenu paperMenu = new();
    int paperIndex;
    readonly Segmented segQuality;
    readonly Label lblCount = Ui.Label(L.T("0 枚", "0 images"), pt: BaseFontPt, fs: FontStyle.Bold, margin: new Padding(4, 0, 6, 0));
    readonly Label lblSelection = Ui.Label("", Fg.SubText, margin: new Padding(0, 0, 16, 0));
    readonly Label lblSummary = Ui.Label("", Fg.SubText, margin: new Padding(0, 0, 12, 0));
    readonly Button btnHistory, btnPaste, btnFile, btnSettings, btnPdf;
    readonly Button btnUp, btnDown, btnRotL, btnRotR, btnDelete, btnClear;
    readonly TableLayoutPanel footer;
    readonly ToastHost toast;

    List<(int Index, ImageEntry Entry)>? undoBuffer;
    bool busy;
    bool uiUpdatePending;

    public MainForm()
    {
        Theme.Dark = Theme.Resolve((ThemeMode)settings.ThemeMode);

        SuspendLayout();
        // 96dpi 基準で設計し、実際の DPI に合わせて自動拡大 (文字欠け防止)
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "ClipBind PDF";
        Theme.Style(this, Bg.Window, Fg.Text);
        Size = new Size(1000, 700);
        MinimumSize = new Size(880, 560);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        AllowDrop = true;

        // ---------- ヘッダー ----------
        var header = Theme.Style(new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1, Padding = new Padding(20, 14, 20, 14), Margin = new Padding(0),
        }, Bg.Surface);
        var titleRow = Theme.Style(new FlowLayoutPanel { AutoSize = true, WrapContents = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 10) }, Bg.Surface);
        titleRow.Controls.Add(Ui.Label("ClipBind PDF", Fg.Text, 15f, FontStyle.Bold, new Padding(0, 0, 12, 0)));
        var subtitle = Ui.Label(L.T("クリップボードの画像をまとめて 1 つの PDF に", "Combine clipboard images into one PDF"), Fg.SubText, margin: new Padding(0, 8, 0, 0));
        subtitle.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
        titleRow.Controls.Add(subtitle);

        var actions = Theme.Style(new FlowLayoutPanel { AutoSize = true, WrapContents = true, Dock = DockStyle.Fill, Margin = new Padding(0) }, Bg.Surface);
        btnHistory = Ui.Button(L.T("クリップボード履歴から取り込み", "Import clipboard history"), Glyph.History, ButtonKind.Standard, async (_, _) => await ImportHistoryAsync(),
            L.T("Win+V のクリップボード履歴にある画像を、コピーした順にまとめて追加します (Ctrl+H)", "Add every image in your Win+V clipboard history, in the order you copied them (Ctrl+H)"), tip);
        btnPaste = Ui.Button(L.T("貼り付け", "Paste"), Glyph.Paste, ButtonKind.Standard, (_, _) => AddFromClipboard(true),
            L.T("今クリップボードにある画像を追加します (Ctrl+V)", "Add the image currently on the clipboard (Ctrl+V)"), tip);
        btnFile = Ui.Button(L.T("ファイルを追加…", "Add files…"), Glyph.Folder, ButtonKind.Standard, (_, _) => AddFromFileDialog(),
            L.T("PNG / JPEG などの画像ファイルを追加します (Ctrl+O)", "Add image files such as PNG or JPEG (Ctrl+O)"), tip);
        btnSettings = Ui.Button(L.T("設定", "Settings"), Glyph.Settings, ButtonKind.Subtle, (_, _) => OpenSettings(),
            L.T("ファイル名・保存先・テーマなどの設定 (Ctrl+,)", "File name, save folder, theme and language (Ctrl+,)"), tip);
        btnSettings.Margin = new Padding(12, 0, 0, 0);
        btnSettings.Anchor = AnchorStyles.Left;
        chkWatch.Checked = settings.AutoAdd;
        chkWatch.Margin = new Padding(16, 0, 0, 0);
        chkWatch.Anchor = AnchorStyles.Left;
        Theme.Style(chkWatch, Bg.Surface);
        chkWatch.CheckedChanged += (_, _) =>
        {
            settings.AutoAdd = chkWatch.Checked;
            toast?.Show(chkWatch.Checked ? L.T("自動追加をオンにしました。画像をコピーするとここに追加されます。", "Auto-add is on. Images you copy will appear here.") : L.T("自動追加をオフにしました。", "Auto-add is off."), ToastKind.Info);
        };
        tip.SetToolTip(chkWatch, L.T("オンの間、ほかのアプリで画像をコピーするたびに一覧へ追加します", "While on, every image you copy in another app is added to the list"));
        actions.Controls.AddRange([btnHistory, btnPaste, btnFile, chkWatch, btnSettings]);
        header.Controls.Add(titleRow);
        header.Controls.Add(actions);

        // ---------- 選択操作バー ----------
        var selBar = Theme.Style(new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, WrapContents = true,
            Padding = new Padding(16, 6, 16, 6), Margin = new Padding(0, 1, 0, 0),
        }, Bg.Surface);
        btnUp = Ui.Button(L.T("前へ", "Earlier"), Glyph.Up, ButtonKind.Subtle, (_, _) => MoveSelected(-1), L.T("選択した画像を前に移動 (Alt+←)", "Move the selected images earlier (Alt+←)"), tip);
        btnDown = Ui.Button(L.T("後へ", "Later"), Glyph.Down, ButtonKind.Subtle, (_, _) => MoveSelected(+1), L.T("選択した画像を後ろに移動 (Alt+→)", "Move the selected images later (Alt+→)"), tip);
        btnRotL = Ui.Button(L.T("左に回転", "Rotate left"), Glyph.Rotate, ButtonKind.Subtle, (_, _) => RotateSelected(RotateFlipType.Rotate270FlipNone), L.T("左に 90° 回転 (Ctrl+L)", "Rotate 90° left (Ctrl+L)"), tip, mirror: true);
        btnRotR = Ui.Button(L.T("右に回転", "Rotate right"), Glyph.Rotate, ButtonKind.Subtle, (_, _) => RotateSelected(RotateFlipType.Rotate90FlipNone), L.T("右に 90° 回転 (Ctrl+R)", "Rotate 90° right (Ctrl+R)"), tip);
        btnDelete = Ui.Button(L.T("削除", "Remove"), Glyph.Delete, ButtonKind.Subtle, (_, _) => RemoveSelected(), L.T("選択した画像を一覧から削除 (Delete)", "Remove the selected images from the list (Delete)"), tip);
        btnClear = Ui.Button(L.T("すべてクリア", "Clear all"), Glyph.Clear, ButtonKind.Subtle, (_, _) => ClearAll(), L.T("一覧を空にします (Ctrl+Z で元に戻せます)", "Empty the list (Ctrl+Z to undo)"), tip);
        btnClear.Margin = new Padding(16, 0, 0, 0);
        foreach (Control c in new Control[] { lblCount, lblSelection, btnUp, btnDown, btnRotL, btnRotR, btnDelete, btnClear })
        {
            c.Anchor = AnchorStyles.Left;
            selBar.Controls.Add(c);
        }

        // ---------- サムネイル一覧 ----------
        list.Dock = DockStyle.Fill;
        list.View = View.LargeIcon;
        list.LargeImageList = thumbs;
        list.MultiSelect = true;
        list.HideSelection = false;
        list.BorderStyle = BorderStyle.None;
        list.AllowDrop = true;
        Theme.Style(list, Bg.Canvas, Fg.Text);
        list.ItemDrag += (_, e) => list.DoDragDrop(e.Item!, DragDropEffects.Move);
        list.DragEnter += OnDragEnter;
        list.DragOver += OnDragOver;
        list.DragDrop += OnDragDrop;
        list.DragLeave += (_, _) => list.InsertionMark.Index = -1;
        list.DoubleClick += (_, _) => PreviewSelected();
        list.SelectedIndexChanged += (_, _) => ScheduleUiUpdate();
        tip.SetToolTip(list, L.T("ドラッグで並べ替え・ダブルクリックで拡大表示", "Drag to reorder · double-click to preview"));

        empty.AllowDrop = true;
        empty.DragEnter += OnDragEnter;
        empty.DragDrop += OnDragDrop;

        var canvas = Theme.Style(new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 8), Margin = new Padding(0, 1, 0, 0) }, Bg.Canvas);
        canvas.Controls.Add(list);
        canvas.Controls.Add(empty);

        // ---------- フッター (PDF 出力) ----------
        footer = Theme.Style(new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 7, RowCount = 1,
            Padding = new Padding(20, 12, 20, 12), Margin = new Padding(0, 1, 0, 0),
        }, Bg.Surface);
        for (int i = 0; i < 7; i++)
            footer.ColumnStyles.Add(i == 4 ? new ColumnStyle(SizeType.Percent, 100) : new ColumnStyle(SizeType.AutoSize));
        segPage = new Segmented(tip,
            ("A4 ▾", L.T("用紙サイズ (クリックで A3・B4・Letter などに変更)。画像の向きに合わせて縦/横を自動で選び、余白 10mm で中央に配置します", "Paper size (click to choose A3, B4, Letter…). Portrait or landscape is chosen per image, centered with 10 mm margins")),
            (L.T("画像サイズ", "Image size"), L.T("ページの大きさを画像そのものの大きさに合わせます (余白なし)", "Each page is exactly the size of its image (no margins)")));
        paperIndex = Math.Clamp(settings.PaperIndex, 0, Paper.All.Length - 1);
        BuildPaperMenu();
        segPage.Buttons[0].Click += (_, _) => paperMenu.Show(segPage.Buttons[0], new Point(0, segPage.Buttons[0].Height));
        segPage.Buttons[0].KeyDown += (_, e) => { if (e.KeyCode is Keys.Down or Keys.F4 || (e.Alt && e.KeyCode == Keys.Down)) { paperMenu.Show(segPage.Buttons[0], new Point(0, segPage.Buttons[0].Height)); e.Handled = true; } };
        segQuality = new Segmented(tip,
            (L.T("高画質", "High"), L.T("JPEG 品質 95。ファイルサイズは大きめ", "JPEG quality 95. Larger files")),
            (L.T("標準", "Standard"), L.T("JPEG 品質 85。おすすめ", "JPEG quality 85. Recommended")),
            (L.T("軽量", "Small"), L.T("JPEG 品質 70。メール添付などに", "JPEG quality 70. Good for email attachments")));
        segPage.SelectedIndex = Math.Clamp(settings.PageMode, 0, 1);
        segQuality.SelectedIndex = Math.Clamp(settings.QualityIndex, 0, 2);
        segPage.SelectedIndexChanged += (_, _) => settings.PageMode = segPage.SelectedIndex;
        segQuality.SelectedIndexChanged += (_, _) => settings.QualityIndex = segQuality.SelectedIndex;
        lblSummary.Anchor = AnchorStyles.Right;
        btnPdf = Ui.Button(L.T("PDF を作成", "Create PDF"), Glyph.Pdf, ButtonKind.Primary, async (_, _) => await SavePdfAsync(), L.T("一覧の順番どおりに PDF を作成します (Ctrl+S)", "Create a PDF in the order shown (Ctrl+S)"), tip);
        btnPdf.Margin = new Padding(0);
        btnPdf.Anchor = AnchorStyles.Right;
        footer.Controls.Add(Ui.Label(L.T("用紙", "Paper"), Fg.SubText), 0, 0);
        footer.Controls.Add(segPage, 1, 0);
        footer.Controls.Add(Ui.Label(L.T("画質", "Quality"), Fg.SubText), 2, 0);
        footer.Controls.Add(segQuality, 3, 0);
        footer.Controls.Add(lblSummary, 5, 0);
        footer.Controls.Add(btnPdf, 6, 0);

        // ---------- 全体 ----------
        var root = Theme.Style(new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = new Padding(0), Padding = new Padding(0) }, Bg.Border);
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(header, 0, 0);
        root.Controls.Add(selBar, 0, 1);
        root.Controls.Add(canvas, 0, 2);
        root.Controls.Add(footer, 0, 3);
        Controls.Add(root);

        ApplyTheme();
        ResumeLayout(false);
        PerformLayout();

        toast = new ToastHost(this, () => footer.Height);
        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;
        KeyDown += OnKeyDown;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        UpdateUiState();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        AddClipboardFormatListener(Handle);
        ApplyTheme();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        RemoveClipboardFormatListener(Handle);
        base.OnHandleDestroyed(e);
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        // WinForms 標準の拡大縮小のあとで、フォント・アイコン・サムネイルを新しい DPI で作り直す
        BeginInvoke(ApplyTheme);
    }

    // ================= テーマ / DPI =================

    /// <summary>配色・フォント・アイコン・サムネイルを、現在のテーマと DPI で適用し直す</summary>
    void BuildPaperMenu()
    {
        paperMenu.Items.Clear();
        for (int i = 0; i < Paper.All.Length; i++)
        {
            int idx = i;
            var p = Paper.All[i];
            var item = new ToolStripMenuItem($"{p.Label}\t{p.Detail}") { Checked = i == paperIndex };
            item.Click += (_, _) => SetPaper(idx);
            paperMenu.Items.Add(item);
        }
        segPage.Buttons[0].Text = Paper.All[paperIndex].Label + " ▾";
    }

    void SetPaper(int idx)
    {
        paperIndex = idx;
        settings.PaperIndex = idx;
        segPage.SelectedIndex = 0;
        for (int i = 0; i < paperMenu.Items.Count; i++)
            ((ToolStripMenuItem)paperMenu.Items[i]).Checked = i == idx;
        segPage.Buttons[0].Text = Paper.All[idx].Label + " ▾";
    }

    void ApplyTheme()
    {
        if (IsDisposed) return;
        int dpi = DeviceDpi;
        SuspendLayout();
        Theme.Apply(this, dpi, BaseFontPt);
        list.InsertionMark.Color = Theme.AccentText;
        if (list.IsHandleCreated) Ui.NativeTheme(list);
        if (IsHandleCreated) Ui.DarkTitleBar(Handle, Theme.Dark);

        // 選択状況の文言が変わってもボタンが左右に動かないよう、ラベル幅を固定
        lblSelection.MinimumSize = new Size(
            new[] { L.T("画像をクリックして選択", "Click an image to select"), L.T("999 枚を選択中", "999 selected") }.Max(t => TextRenderer.MeasureText(t, lblSelection.Font).Width), 0);

        // ボタンの高さを揃える (ヘッダー / フッター)
        Ui.EqualizeHeights([btnHistory, btnPaste, btnFile]);
        Ui.EqualizeHeights(segPage.Buttons.Concat(segQuality.Buttons).Append<Control>(btnPdf));
        paperMenu.ApplyTheme(dpi);
        // 用紙名が変わってもボタン幅が変わらないよう、いちばん長い名前に合わせる
        var pb = segPage.Buttons[0];
        pb.MinimumSize = new Size(Paper.All.Max(p => TextRenderer.MeasureText(p.Label + " ▾", pb.Font).Width) + pb.Padding.Horizontal + 4, pb.MinimumSize.Height);

        int px = ThumbPx;
        if (thumbs.ImageSize.Width != px)
        {
            var entries = Entries().ToList();
            thumbs.Images.Clear();
            thumbs.ImageSize = new Size(px, px);
            foreach (var en in entries) thumbs.Images.Add(en.Key, MakeThumb(en.Image, px));
            list.LargeImageList = thumbs;
        }
        ResumeLayout(true);

        if (list.IsHandleCreated)
        {
            int cx = px + LogicalToDeviceUnits(24), cy = px + list.Font.Height * 2 + LogicalToDeviceUnits(18);
            SendMessage(list.Handle, LVM_SETICONSPACING, IntPtr.Zero, (IntPtr)((cy << 16) | (cx & 0xFFFF)));
            RefreshLayout();
        }
        Invalidate(true);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (!settings.CheckUpdates) return;
        if (settings.LastUpdateCheck is { } last && DateTime.Now - last < TimeSpan.FromHours(20)) return;
        await Task.Delay(3000); // 起動直後の作業を邪魔しない
        try
        {
            var info = await Updater.CheckAsync();
            settings.LastUpdateCheck = DateTime.Now;
            settings.Save();
            if (info != null && settings.SkippedVersion != info.Version.ToString() && !IsDisposed)
                ShowUpdateToast(info);
        }
        catch { /* オフラインなどは黙って次回に回す */ }
    }

    void ShowUpdateToast(UpdateInfo info)
    {
        string v = info.Version.ToString();
        toast.Show(L.T($"新しいバージョン {v} があります（いまは {Updater.CurrentText}）。",
                       $"Version {v} is available (you have {Updater.CurrentText})."), ToastKind.Info,
            new ToastAction(L.T("ダウンロード", "Download"), () => Updater.Open(info.InstallerUrl ?? info.PageUrl)),
            new ToastAction(L.T("変更点", "What's new"), () => Updater.Open(info.PageUrl)),
            new ToastAction(L.T("スキップ", "Skip"), () => { settings.SkippedVersion = v; settings.Save(); }));
    }

    void OpenSettings()
    {
        using var dlg = new SettingsForm(settings, DeviceDpi);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var r = dlg.Result;
        settings.FileNameTemplate = r.FileNameTemplate;
        settings.SaveFolder = r.SaveFolder;
        settings.AskSaveLocation = r.AskSaveLocation;
        settings.OpenAfterSave = r.OpenAfterSave;
        settings.ThemeMode = r.ThemeMode;
        settings.CheckUpdates = r.CheckUpdates;
        settings.LastUpdateCheck = r.LastUpdateCheck ?? settings.LastUpdateCheck;
        bool langChanged = settings.Language != r.Language;
        settings.Language = r.Language;
        settings.Save();
        SetDark(Theme.Resolve((ThemeMode)settings.ThemeMode));
        if (langChanged)
            toast.Show(L.T("設定を保存しました。言語は次回の起動から切り替わります。",
                "Settings saved. The language changes the next time you start the app."), ToastKind.Success);
        else
            toast.Show(L.T("設定を保存しました。", "Settings saved."), ToastKind.Success);
    }

    void SetDark(bool dark)
    {
        if (Theme.Dark == dark) return;
        Theme.Dark = dark;
        toast.CloseAll();
        ApplyTheme();
    }

    void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        // Windows 側でライト/ダークが切り替わったら追従 (テーマ「自動」のときのみ)
        if (settings.ThemeMode != (int)ThemeMode.System || IsDisposed || !IsHandleCreated) return;
        BeginInvoke(() => SetDark(Theme.SystemPrefersDark()));
    }

    int ThumbPx => Math.Min(256, LogicalToDeviceUnits(148));

    // ================= クリップボード監視 =================

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_CLIPBOARDUPDATE && chkWatch.Checked && !busy)
            BeginInvoke(() => AddFromClipboard(false));
        base.WndProc(ref m);
    }

    void AddFromClipboard(bool userAction)
    {
        try
        {
            if (Clipboard.ContainsImage())
            {
                var img = Clipboard.GetImage();
                if (img == null) return;
                if (AddImage(img, skipDuplicateOfLast: !userAction))
                {
                    AfterAdd();
                    toast.ShowKeyed("added", L.T($"画像を追加しました（全 {list.Items.Count} 枚）", $"Image added ({list.Items.Count} total)"), ToastKind.Success);
                }
            }
            else if (Clipboard.ContainsFileDropList())
            {
                int n = AddFiles(Clipboard.GetFileDropList().Cast<string>());
                if (userAction && n == 0) toast.Show(L.T("クリップボードのファイルに画像が含まれていません。", "The files on the clipboard contain no images."), ToastKind.Warning);
            }
            else if (userAction)
            {
                toast.Show(L.T("クリップボードに画像がありません。画像をコピーしてからもう一度お試しください。", "There is no image on the clipboard. Copy an image and try again."), ToastKind.Warning);
            }
        }
        catch (ExternalException)
        {
            if (userAction) toast.Show(L.T("クリップボードを読み取れませんでした。少し待ってからもう一度お試しください。", "Could not read the clipboard. Wait a moment and try again."), ToastKind.Error);
        }
    }

    // ================= クリップボード履歴 (Win+V) =================

    async Task ImportHistoryAsync()
    {
        if (busy) return;
        try
        {
            if (!WinClipboard.IsHistoryEnabled())
            {
                toast.Show(L.T("Windows のクリップボード履歴がオフになっています。", "Windows clipboard history is turned off."), ToastKind.Warning,
                    new ToastAction(L.T("設定を開く", "Open settings"), () => Process.Start(new ProcessStartInfo("ms-settings:clipboard") { UseShellExecute = true })));
                return;
            }

            SetBusy(true);
            var result = await WinClipboard.GetHistoryItemsAsync();
            if (result.Status != Windows.ApplicationModel.DataTransfer.ClipboardHistoryItemsResultStatus.Success)
            {
                toast.Show(L.T($"クリップボード履歴を取得できませんでした（{result.Status}）。", $"Could not get the clipboard history ({result.Status})."), ToastKind.Error);
                return;
            }

            // 履歴は新しい順 → コピーした順 (古い順) に並べる
            var loaded = new List<Image>();
            foreach (var item in result.Items.Reverse())
            {
                if (!item.Content.Contains(StandardDataFormats.Bitmap)) continue;
                try
                {
                    var streamRef = await item.Content.GetBitmapAsync();
                    using var ras = await streamRef.OpenReadAsync();
                    using var s = ras.AsStreamForRead();
                    using var ms = new MemoryStream();
                    await s.CopyToAsync(ms);
                    ms.Position = 0;
                    using var tmp = Image.FromStream(ms);
                    loaded.Add(new Bitmap(tmp));
                }
                catch { /* 読めない項目はスキップ */ }
            }

            if (loaded.Count == 0)
            {
                toast.Show(L.T("クリップボード履歴に画像がありませんでした。", "There are no images in the clipboard history."), ToastKind.Info);
                return;
            }

            int added = 0;
            list.BeginUpdate();
            foreach (var img in loaded)
                if (AddImage(img, skipDuplicateAny: true)) added++;
            list.EndUpdate();
            AfterAdd();

            int skipped = loaded.Count - added;
            if (added == 0)
                toast.Show(L.T($"履歴の画像 {loaded.Count} 枚はすべて追加済みです。", $"All {loaded.Count} images in the history are already added."), ToastKind.Info);
            else
                toast.Show(L.T($"履歴から {added} 枚を取り込みました。" + (skipped > 0 ? $"（重複 {skipped} 枚はスキップ）" : ""), $"Imported {added} images from history." + (skipped > 0 ? $" ({skipped} duplicates skipped)" : "")), ToastKind.Success);
        }
        catch (Exception ex)
        {
            toast.Show(L.T("クリップボード履歴の読み込みに失敗しました：" + ex.Message, "Failed to read the clipboard history: " + ex.Message), ToastKind.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ================= 画像の追加・管理 =================

    bool AddImage(Image img, bool skipDuplicateOfLast = false, bool skipDuplicateAny = false)
    {
        string hash = ComputeHash(img);
        var entries = Entries().ToList();
        if ((skipDuplicateAny && entries.Any(e => e.Hash == hash)) ||
            (skipDuplicateOfLast && entries.Count > 0 && entries[^1].Hash == hash))
        {
            img.Dispose();
            return false;
        }

        var entry = new ImageEntry { Image = img, Hash = hash };
        thumbs.Images.Add(entry.Key, MakeThumb(img, ThumbPx));
        list.Items.Add(new ListViewItem { ImageKey = entry.Key, Tag = entry });
        return true;
    }

    void AfterAdd()
    {
        Renumber();
        if (list.Items.Count > 0)
        {
            var last = list.Items[^1];
            list.SelectedItems.Clear();
            last.Selected = true;
            last.Focused = true;
            last.EnsureVisible();
        }
    }

    int AddFiles(IEnumerable<string> paths)
    {
        string[] exts = [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff"];
        int n = 0, failed = 0;
        list.BeginUpdate();
        foreach (var p in paths.Where(p => exts.Contains(Path.GetExtension(p).ToLowerInvariant())))
        {
            try
            {
                using var fs = File.OpenRead(p);
                using var tmp = Image.FromStream(fs);
                if (AddImage(new Bitmap(tmp))) n++;
            }
            catch { failed++; }
        }
        list.EndUpdate();
        if (n > 0)
        {
            AfterAdd();
            toast.Show(L.T($"{n} 枚の画像を追加しました。" + (failed > 0 ? $"（読み込めなかったファイル {failed} 件）" : ""), $"Added {n} images." + (failed > 0 ? $" ({failed} files could not be read)" : "")), ToastKind.Success);
        }
        else if (failed > 0)
            toast.Show(L.T("画像ファイルを読み込めませんでした。", "Could not read the image files."), ToastKind.Error);
        return n;
    }

    void AddFromFileDialog()
    {
        using var dlg = new OpenFileDialog
        {
            Multiselect = true,
            Title = L.T("追加する画像を選択", "Choose images to add"),
            Filter = L.T("画像ファイル|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|すべてのファイル|*.*", "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|All files|*.*"),
        };
        if (dlg.ShowDialog(this) == DialogResult.OK) AddFiles(dlg.FileNames);
    }

    IEnumerable<ImageEntry> Entries() => list.Items.Cast<ListViewItem>().Select(i => (ImageEntry)i.Tag!);

    void Renumber()
    {
        for (int i = 0; i < list.Items.Count; i++)
        {
            var e = (ImageEntry)list.Items[i].Tag!;
            list.Items[i].Text = $"{i + 1}\n{e.Image.Width} × {e.Image.Height}";
        }
        UpdateUiState();
    }

    /// <summary>
    /// 画像をクリックすると ListView は「選択解除 → 選択」と続けて通知してくるため、
    /// その都度ボタンを無効→有効にするとちらつく。まとめて 1 回だけ更新する。
    /// </summary>
    void ScheduleUiUpdate()
    {
        if (uiUpdatePending || !IsHandleCreated) return;
        uiUpdatePending = true;
        BeginInvoke(() => { uiUpdatePending = false; UpdateUiState(); });
    }

    void UpdateUiState()
    {
        int count = list.Items.Count, sel = list.SelectedIndices.Count;
        bool has = count > 0;
        empty.Visible = !has;
        list.Visible = has;
        lblCount.Text = L.T($"{count} 枚", $"{count} images");
        lblSelection.Text = sel > 0 ? L.T($"{sel} 枚を選択中", $"{sel} selected") : has ? L.T("画像をクリックして選択", "Click an image to select") : "";
        btnUp.Enabled = btnDown.Enabled = btnRotL.Enabled = btnRotR.Enabled = btnDelete.Enabled = sel > 0 && !busy;
        btnClear.Enabled = has && !busy;
        btnPdf.Enabled = has && !busy;
        lblSummary.Text = has ? L.T($"{count} ページ", $"{count} pages") : L.T("画像を追加してください", "Add some images");
        btnHistory.Enabled = btnPaste.Enabled = btnFile.Enabled = !busy;
    }

    void SetBusy(bool value)
    {
        busy = value;
        UseWaitCursor = value;
        UpdateUiState();
    }

    void MoveSelected(int dir)
    {
        var idx = list.SelectedIndices.Cast<int>().OrderBy(i => i).ToList();
        if (idx.Count == 0) return;
        if (dir > 0) idx.Reverse();
        if ((dir < 0 && idx[0] == 0) || (dir > 0 && idx[0] == list.Items.Count - 1)) return;

        list.BeginUpdate();
        foreach (int i in idx)
        {
            var item = list.Items[i];
            list.Items.RemoveAt(i);
            list.Items.Insert(i + dir, item);
            item.Selected = true;
        }
        list.EndUpdate();
        Renumber();
        RefreshLayout();
        list.SelectedItems[0].EnsureVisible();
    }

    void RotateSelected(RotateFlipType rf)
    {
        foreach (ListViewItem item in list.SelectedItems)
        {
            var e = (ImageEntry)item.Tag!;
            e.Image.RotateFlip(rf);
            int ix = thumbs.Images.IndexOfKey(e.Key);
            if (ix >= 0) thumbs.Images[ix] = MakeThumb(e.Image, ThumbPx);
        }
        Renumber();
        list.Invalidate();
    }

    void RemoveSelected() => RemoveItems(list.SelectedItems.Cast<ListViewItem>().ToList());

    void ClearAll() => RemoveItems(list.Items.Cast<ListViewItem>().ToList());

    /// <summary>削除は確認ダイアログを出さず、トーストの「元に戻す」で取り消せるようにする</summary>
    void RemoveItems(List<ListViewItem> items)
    {
        if (items.Count == 0) return;
        DiscardUndo();
        undoBuffer = items.Select(i => (i.Index, (ImageEntry)i.Tag!)).OrderBy(x => x.Index).ToList();
        list.BeginUpdate();
        foreach (var item in items)
        {
            var e = (ImageEntry)item.Tag!;
            list.Items.Remove(item);
            thumbs.Images.RemoveByKey(e.Key);
        }
        list.EndUpdate();
        Renumber();
        RefreshLayout();
        toast.Show(L.T($"{items.Count} 枚を削除しました。", $"Removed {items.Count} images."), ToastKind.Info, new ToastAction(L.T("元に戻す", "Undo"), Undo));
    }

    void Undo()
    {
        if (undoBuffer == null) return;
        list.BeginUpdate();
        list.SelectedItems.Clear();
        foreach (var (index, entry) in undoBuffer)
        {
            thumbs.Images.Add(entry.Key, MakeThumb(entry.Image, ThumbPx));
            var lvi = new ListViewItem { ImageKey = entry.Key, Tag = entry };
            list.Items.Insert(Math.Min(index, list.Items.Count), lvi);
            lvi.Selected = true;
        }
        list.EndUpdate();
        int n = undoBuffer.Count;
        undoBuffer = null;
        Renumber();
        RefreshLayout();
        toast.Show(L.T($"{n} 枚を元に戻しました。", $"Restored {n} images."), ToastKind.Success);
    }

    void DiscardUndo()
    {
        if (undoBuffer == null) return;
        foreach (var (_, e) in undoBuffer) e.Dispose();
        undoBuffer = null;
    }

    void RefreshLayout()
    {
        // LargeIcon 表示で並び順を反映させる
        list.BeginUpdate();
        list.View = View.Tile;
        list.View = View.LargeIcon;
        list.EndUpdate();
    }

    void PreviewSelected()
    {
        if (list.SelectedItems.Count == 0) return;
        var item = list.SelectedItems[0];
        var e = (ImageEntry)item.Tag!;
        using var f = new Form
        {
            Text = L.T($"プレビュー - {item.Index + 1} ページ目  ({e.Image.Width} × {e.Image.Height})", $"Preview - page {item.Index + 1}  ({e.Image.Width} × {e.Image.Height})"),
            Size = new Size(LogicalToDeviceUnits(960), LogicalToDeviceUnits(720)),
            StartPosition = FormStartPosition.CenterParent,
            KeyPreview = true,
            Icon = Icon,
            ShowInTaskbar = false,
        };
        f.HandleCreated += (_, _) => Ui.DarkTitleBar(f.Handle, Theme.Dark);
        f.KeyDown += (_, k) => { if (k.KeyCode == Keys.Escape) f.Close(); };
        f.Controls.Add(new PictureBox { Dock = DockStyle.Fill, Image = e.Image, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(32, 32, 34) });
        f.ShowDialog(this);
    }

    // ================= ドラッグ&ドロップ =================

    void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data!.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        else if (e.Data.GetDataPresent(typeof(ListViewItem))) e.Effect = DragDropEffects.Move;
    }

    void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data!.GetDataPresent(typeof(ListViewItem)))
        {
            e.Effect = DragDropEffects.Move;
            var pt = list.PointToClient(new Point(e.X, e.Y));
            int i = list.InsertionMark.NearestIndex(pt);
            if (i >= 0)
            {
                var r = list.GetItemRect(i);
                list.InsertionMark.AppearsAfterItem = pt.X > r.Left + r.Width / 2;
            }
            list.InsertionMark.Index = i;
        }
        else OnDragEnter(sender, e);
    }

    void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data!.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            if (AddFiles(files) == 0 && files.Length > 0)
                toast.Show(L.T("対応している画像ファイル（PNG / JPEG / BMP / GIF / TIFF）をドロップしてください。", "Drop supported image files (PNG / JPEG / BMP / GIF / TIFF)."), ToastKind.Warning);
            return;
        }
        if (e.Data.GetData(typeof(ListViewItem)) is ListViewItem dragged)
        {
            int target = list.InsertionMark.Index;
            bool after = list.InsertionMark.AppearsAfterItem;
            list.InsertionMark.Index = -1;
            if (target < 0) return;
            if (after) target++;
            int from = dragged.Index;
            if (target > from) target--;
            if (target == from) return;
            list.BeginUpdate();
            list.Items.Remove(dragged);
            list.Items.Insert(Math.Min(target, list.Items.Count), dragged);
            list.EndUpdate();
            Renumber();
            RefreshLayout();
        }
    }

    void OnKeyDown(object? sender, KeyEventArgs e)
    {
        bool handled = true;
        if (e.Control && e.KeyCode == Keys.V) AddFromClipboard(true);
        else if (e.Control && e.KeyCode == Keys.Z) Undo();
        else if (e.Control && e.KeyCode == Keys.S) { if (btnPdf.Enabled) _ = SavePdfAsync(); }
        else if (e.Control && e.KeyCode == Keys.O) AddFromFileDialog();
        else if (e.Control && e.KeyCode == Keys.H) _ = ImportHistoryAsync();
        else if (e.Control && e.KeyCode == Keys.Oemcomma) OpenSettings();
        else if (e.Control && e.KeyCode == Keys.A) { foreach (ListViewItem i in list.Items) i.Selected = true; }
        else if (e.Control && e.KeyCode == Keys.L) RotateSelected(RotateFlipType.Rotate270FlipNone);
        else if (e.Control && e.KeyCode == Keys.R) RotateSelected(RotateFlipType.Rotate90FlipNone);
        else if (e.Alt && e.KeyCode is Keys.Left or Keys.Up) MoveSelected(-1);
        else if (e.Alt && e.KeyCode is Keys.Right or Keys.Down) MoveSelected(+1);
        else if (e.KeyCode == Keys.Delete) RemoveSelected();
        else if (e.KeyCode == Keys.Enter && list.Focused) PreviewSelected();
        else handled = false;
        if (handled) { e.Handled = true; e.SuppressKeyPress = true; }
    }

    // ================= PDF 出力 =================

    async Task SavePdfAsync()
    {
        if (busy) return;
        if (list.Items.Count == 0)
        {
            toast.Show(L.T("PDF にする画像がありません。先に画像を追加してください。", "There are no images to make a PDF from. Add some images first."), ToastKind.Warning);
            return;
        }
        string fileName = AppSettings.ExpandFileName(settings.FileNameTemplate, list.Items.Count, DateTime.Now);
        string path;
        if (settings.AskSaveLocation)
        {
            using var dlg = new SaveFileDialog
            {
                Title = L.T("PDF の保存先", "Save PDF"),
                Filter = L.T("PDF ファイル|*.pdf", "PDF files|*.pdf"),
                FileName = fileName,
                // 設定の保存先 (未設定ならダウンロード) から開く
                InitialDirectory = settings.EffectiveSaveFolder,
                RestoreDirectory = true,
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            path = dlg.FileName;
            settings.LastFolder = Path.GetDirectoryName(path);
        }
        else
        {
            var folder = settings.SaveFolder is { Length: > 0 } f ? f : settings.EffectiveSaveFolder;
            try { Directory.CreateDirectory(folder); }
            catch
            {
                toast.Show(L.T($"保存先フォルダーを使えません：{folder}\n設定で保存先を変更してください。", $"Cannot use the save folder: {folder}\nChange it in Settings."), ToastKind.Error,
                    new ToastAction(L.T("設定を開く", "Open settings"), OpenSettings));
                return;
            }
            path = AppSettings.UniquePath(folder, fileName);
        }

        var images = Entries().Select(x => x.Image).ToList();
        Paper? paper = segPage.SelectedIndex == 1 ? null : Paper.All[paperIndex];
        long quality = QualityValues[Math.Clamp(segQuality.SelectedIndex, 0, 2)];
        string oldText = btnPdf.Text;
        try
        {
            SetBusy(true);
            btnPdf.Text = L.T("作成中…", "Creating…");
            await Task.Run(() => PdfWriter.Write(path, images, paper, quality));
            long size = new FileInfo(path).Length;
            if (settings.OpenAfterSave)
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            toast.Show(L.T($"PDF を保存しました（{images.Count} ページ・{FormatSize(size)}）\n{Path.GetFileName(path)}", $"PDF saved ({images.Count} pages · {FormatSize(size)})\n{Path.GetFileName(path)}"), ToastKind.Success,
                new ToastAction(L.T("開く", "Open"), () => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })),
                new ToastAction(L.T("フォルダーを表示", "Show in folder"), () => Process.Start("explorer.exe", $"/select,\"{path}\"")));
        }
        catch (IOException ex)
        {
            toast.Show(L.T("PDF を保存できませんでした。ほかのアプリで開いていないか確認してください。\n" + ex.Message, "Could not save the PDF. Make sure it is not open in another app.\n" + ex.Message), ToastKind.Error);
        }
        catch (Exception ex)
        {
            toast.Show(L.T("PDF の作成に失敗しました：" + ex.Message, "Failed to create the PDF: " + ex.Message), ToastKind.Error);
        }
        finally
        {
            btnPdf.Text = oldText;
            SetBusy(false);
        }
    }

    static string FormatSize(long bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / 1024.0 / 1024.0:0.0} MB" : $"{Math.Max(1, bytes / 1024)} KB";

    // ================= ユーティリティ =================

    static Bitmap MakeThumb(Image img, int size)
    {
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        int pad = Math.Max(4, size / 24);
        double s = Math.Min((size - pad * 2.0) / img.Width, (size - pad * 2.0) / img.Height);
        int w = Math.Max(1, (int)(img.Width * s)), h = Math.Max(1, (int)(img.Height * s));
        var rect = new Rectangle((size - w) / 2, (size - h) / 2, w, h);
        using (var shadow = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
            g.FillRectangle(shadow, rect.X + 2, rect.Y + 2, rect.Width, rect.Height);
        g.FillRectangle(Brushes.White, rect);
        g.DrawImage(img, rect);
        using var pen = new Pen(Color.FromArgb(140, 128, 128, 136));
        g.DrawRectangle(pen, rect);
        return bmp;
    }

    static string ComputeHash(Image img)
    {
        using var bmp = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp)) g.DrawImage(img, 0, 0, img.Width, img.Height);
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, bmp.PixelFormat);
        try
        {
            var bytes = new byte[data.Stride * data.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
        finally { bmp.UnlockBits(data); }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        settings.Save();
        DiscardUndo();
        foreach (var x in Entries().ToList()) x.Dispose();
        base.OnFormClosed(e);
    }
}
