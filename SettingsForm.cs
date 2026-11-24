namespace ClipboardPDF;

/// <summary>設定画面: 既定のファイル名・保存先・保存後の動作・テーマ・言語</summary>
sealed class SettingsForm : Form
{
    readonly AppSettings draft;
    readonly TextBox txtName;
    readonly Label lblPreview;
    readonly ToggleSwitch tglAsk;
    readonly ToggleSwitch tglOpen;
    readonly TextBox txtFolder;
    readonly Button btnBrowse;
    readonly Segmented segTheme;
    readonly Segmented segLang;
    readonly ToolTip tip = Ui.ToolTip();
    readonly ToggleSwitch tglUpdates;
    readonly Button btnCheck;
    readonly Label lblUpdate;
    readonly Button lnkUpdate;
    UpdateInfo? found;

    public AppSettings Result => draft;

    public SettingsForm(AppSettings current, int dpi)
    {
        draft = current.Clone();

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = L.T("設定 - ClipBind PDF", "Settings - ClipBind PDF");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        KeyPreview = true;
        Theme.Style(this, Bg.Surface, Fg.Text);

        var root = Theme.Style(new TableLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2,
            Padding = new Padding(24, 20, 24, 16), Margin = new Padding(0),
        }, Bg.Surface);
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        void Section(string title, bool first = false)
        {
            var l = Ui.Label(title, Fg.Text, 11.5f, FontStyle.Bold, new Padding(0, first ? 0 : 18, 0, 8));
            root.Controls.Add(l);
            root.SetColumnSpan(l, 2);
        }
        void Row(string label, Control control)
        {
            var l = Ui.Label(label, Fg.SubText, margin: new Padding(0, 0, 16, 8));
            l.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            l.Padding = new Padding(0, 5, 0, 0);
            control.Margin = new Padding(0, 0, 0, 8);
            root.Controls.Add(l);
            root.Controls.Add(control);
        }
        FlowLayoutPanel Stack() => Theme.Style(new FlowLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, Margin = new Padding(0), Padding = new Padding(0),
        }, Bg.Surface);
        FlowLayoutPanel Line() => Theme.Style(new FlowLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false,
            Margin = new Padding(0), Padding = new Padding(0),
        }, Bg.Surface);

        // ---------- PDF の保存 ----------
        Section(L.T("PDF の保存", "Saving PDFs"), first: true);

        var nameStack = Stack();
        var nameLine = Line();
        txtName = Theme.Style(new TextBox { Width = 340, Text = draft.FileNameTemplate, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 0, 4, 0) });
        var ext = Ui.Label(".pdf", Fg.SubText, margin: new Padding(0, 4, 0, 0));
        nameLine.Controls.AddRange([txtName, ext]);
        var tokens = Line();
        tokens.Margin = new Padding(0, 6, 0, 0);
        tokens.Controls.Add(Ui.Label(L.T("挿入:", "Insert:"), Fg.SubText, margin: new Padding(0, 4, 4, 0)));
        foreach (var (token, desc) in new[]
        {
            ("{date}", L.T("日付 (例: 20261124) を挿入", "Insert the date (e.g. 20261124)")),
            ("{time}", L.T("時刻 (例: 135955) を挿入", "Insert the time (e.g. 135955)")),
            ("{pages}", L.T("ページ数を挿入", "Insert the page count")),
        })
        {
            var b = Ui.Button(token, null, ButtonKind.Link, (_, _) => InsertToken(token), desc, tip);
            Theme.Style(b, pt: 10f);
            tokens.Controls.Add(b);
        }
        lblPreview = Ui.Label("", Fg.SubText, margin: new Padding(0, 6, 0, 0));
        var reset = Ui.Button(L.T("既定に戻す", "Reset"), null, ButtonKind.Link, (_, _) => txtName.Text = AppSettings.DefaultFileNameTemplate);
        Theme.Style(reset, pt: 10f);
        tokens.Controls.Add(reset);
        nameStack.Controls.AddRange([nameLine, tokens, lblPreview]);
        Row(L.T("ファイル名", "File name"), nameStack);
        txtName.TextChanged += (_, _) => UpdatePreview();

        var folderStack = Stack();
        var folderLine = Line();
        txtFolder = Theme.Style(new TextBox
        {
            Width = 340, ReadOnly = true, Text = draft.EffectiveSaveFolder,
            BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 0, 8, 0), TabStop = false,
        });
        btnBrowse = Ui.Button(L.T("参照…", "Browse…"), Glyph.Folder, ButtonKind.Standard, (_, _) => BrowseFolder());
        btnBrowse.Margin = new Padding(0);
        folderLine.Controls.AddRange([txtFolder, btnBrowse]);
        tglAsk = new ToggleSwitch
        {
            Text = L.T("保存のたびに保存先とファイル名を確認する", "Ask where to save and the file name every time"),
            Checked = draft.AskSaveLocation, Margin = new Padding(0, 8, 0, 0),
        };
        Theme.Style(tglAsk, Bg.Surface);
        tip.SetToolTip(tglAsk, L.T("オフにすると、ダイアログを出さずに上のフォルダーへすぐ保存します",
            "When off, PDFs are saved straight to the folder above without a dialog"));
        folderStack.Controls.AddRange([folderLine, tglAsk]);
        Row(L.T("保存先", "Save to"), folderStack);

        tglOpen = new ToggleSwitch { Text = L.T("保存したら PDF を開く", "Open the PDF after saving"), Checked = draft.OpenAfterSave };
        Theme.Style(tglOpen, Bg.Surface);
        Row(L.T("保存後", "After saving"), tglOpen);

        // ---------- 表示 ----------
        Section(L.T("表示", "Appearance"));
        segTheme = new Segmented(tip,
            (L.T("自動", "Auto"), L.T("Windows の設定 (ライト/ダーク) に合わせます", "Follow the Windows light/dark setting")),
            (L.T("ライト", "Light"), L.T("常に明るい表示", "Always light")),
            (L.T("ダーク", "Dark"), L.T("常に暗い表示", "Always dark")));
        segTheme.SelectedIndex = Math.Clamp(draft.ThemeMode, 0, 2);
        Row(L.T("テーマ", "Theme"), segTheme);

        segLang = new Segmented(tip,
            (L.T("自動", "Auto"), L.T("Windows の表示言語に合わせます", "Follow the Windows display language")),
            ("日本語", "日本語で表示します"),
            ("English", "Show the app in English"));
        segLang.SelectedIndex = Math.Clamp(draft.Language, 0, 2);
        Row(L.T("言語", "Language"), segLang);

        // ---------- 更新 ----------
        Section(L.T("更新", "Updates"));
        var updStack = Stack();
        var updLine = Line();
        updLine.Controls.Add(Ui.Label(L.T($"バージョン {Updater.CurrentText}", $"Version {Updater.CurrentText}"), Fg.Text, margin: new Padding(0, 5, 12, 0)));
        btnCheck = Ui.Button(L.T("今すぐ確認", "Check now"), null, ButtonKind.Standard, async (_, _) => await CheckNowAsync());
        btnCheck.Margin = new Padding(0);
        updLine.Controls.Add(btnCheck);
        lblUpdate = Ui.Label("", Fg.SubText, margin: new Padding(0, 6, 0, 0));
        lnkUpdate = Ui.Button("", null, ButtonKind.Link, (_, _) => { if (found != null) Updater.Open(found.InstallerUrl ?? found.PageUrl); });
        lnkUpdate.Visible = false;
        tglUpdates = new ToggleSwitch
        {
            Text = L.T("起動時に新しいバージョンを確認する", "Check for a new version at startup"),
            Checked = draft.CheckUpdates, Margin = new Padding(0, 8, 0, 0),
        };
        Theme.Style(tglUpdates, Bg.Surface);
        tip.SetToolTip(tglUpdates, L.T("1 日 1 回、GitHub の公開リリースを確認します。送るのはアプリ名とバージョンだけです",
            "Once a day, checks the public releases on GitHub. Only the app name and version are sent"));
        updStack.Controls.AddRange([updLine, lblUpdate, lnkUpdate, tglUpdates]);
        Row(L.T("バージョン", "Version"), updStack);

        // ---------- ボタン ----------
        var buttons = Line();
        buttons.Anchor = AnchorStyles.Right;
        buttons.Margin = new Padding(0, 20, 0, 0);
        var ok = Ui.Button(L.T("保存", "Save"), null, ButtonKind.Primary, (_, _) => Commit());
        var cancel = Ui.Button(L.T("キャンセル", "Cancel"), null, ButtonKind.Standard, (_, _) => { DialogResult = DialogResult.Cancel; Close(); });
        ok.Margin = new Padding(0, 0, 8, 0);
        cancel.Margin = new Padding(0);
        buttons.Controls.AddRange([ok, cancel]);
        root.Controls.Add(buttons);
        root.SetColumnSpan(buttons, 2);

        Controls.Add(root);
        AcceptButton = ok;
        CancelButton = cancel;

        Theme.Apply(this, dpi, 10f);
        ResumeLayout(false);
        PerformLayout();
        UpdatePreview();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Ui.DarkTitleBar(Handle, Theme.Dark);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // 開いた瞬間にファイル名が全選択されて誤って消さないよう、カーソルを末尾に置く
        txtName.SelectionStart = txtName.TextLength;
        txtName.SelectionLength = 0;
    }

    void InsertToken(string token)
    {
        int pos = txtName.SelectionStart;
        txtName.SelectedText = token;
        txtName.Focus();
        txtName.SelectionStart = pos + token.Length;
    }

    void UpdatePreview() =>
        lblPreview.Text = L.T("例: ", "e.g. ") + AppSettings.ExpandFileName(txtName.Text, 5, DateTime.Now);

    void BrowseFolder()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = L.T("PDF の保存先フォルダーを選択", "Choose a folder to save PDFs in"),
            UseDescriptionForTitle = true,
            InitialDirectory = txtFolder.Text,
        };
        if (dlg.ShowDialog(this) == DialogResult.OK) txtFolder.Text = dlg.SelectedPath;
    }

    async Task CheckNowAsync()
    {
        btnCheck.Enabled = false;
        lnkUpdate.Visible = false;
        lblUpdate.Text = L.T("確認しています…", "Checking…");
        try
        {
            found = await Updater.CheckAsync();
            draft.LastUpdateCheck = DateTime.Now;
            if (IsDisposed) return;
            if (found == null)
                lblUpdate.Text = L.T("最新のバージョンです。", "You have the latest version.");
            else
            {
                lblUpdate.Text = L.T($"新しいバージョン {found.Version} があります。", $"Version {found.Version} is available.");
                lnkUpdate.Text = L.T($"{found.Version} をダウンロード", $"Download {found.Version}");
                lnkUpdate.Visible = true;
            }
        }
        catch
        {
            if (!IsDisposed) lblUpdate.Text = L.T("確認できませんでした。インターネット接続を確認してください。", "Could not check. Check your internet connection.");
        }
        finally
        {
            if (!IsDisposed) btnCheck.Enabled = true;
        }
    }

    void Commit()
    {
        draft.FileNameTemplate = string.IsNullOrWhiteSpace(txtName.Text) ? AppSettings.DefaultFileNameTemplate : txtName.Text.Trim();
        draft.SaveFolder = txtFolder.Text;
        draft.AskSaveLocation = tglAsk.Checked;
        draft.OpenAfterSave = tglOpen.Checked;
        draft.ThemeMode = segTheme.SelectedIndex;
        draft.Language = segLang.SelectedIndex;
        draft.CheckUpdates = tglUpdates.Checked;
        DialogResult = DialogResult.OK;
        Close();
    }
}
