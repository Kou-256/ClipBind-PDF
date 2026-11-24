using System.Text.Json;

namespace ClipboardPDF;

/// <summary>設定を %AppData%\ClipBind PDF\settings.json に保存</summary>
sealed class AppSettings
{
    public const string DefaultFileNameTemplate = "ClipBind_{date}_{time}";

    public int PageMode { get; set; }          // 0 = 用紙, 1 = 画像サイズ
    public int PaperIndex { get; set; } = Paper.DefaultIndex; // Paper.All のインデックス
    public int QualityIndex { get; set; } = 1; // 0 = 高画質, 1 = 標準, 2 = 軽量
    public bool AutoAdd { get; set; } = true;
    public int ThemeMode { get; set; }         // 0 = Windows に合わせる, 1 = ライト, 2 = ダーク
    public int Language { get; set; }          // 0 = Windows に合わせる, 1 = 日本語, 2 = English
    public string? LastFolder { get; set; }

    /// <summary>既定のファイル名。{date} {time} {pages} が使える</summary>
    public string FileNameTemplate { get; set; } = DefaultFileNameTemplate;
    /// <summary>true: 保存のたびに保存先を確認 / false: SaveFolder にそのまま保存</summary>
    public bool AskSaveLocation { get; set; } = true;
    /// <summary>既定の保存先 (null ならダウンロード)</summary>
    public string? SaveFolder { get; set; }
    public bool OpenAfterSave { get; set; }
    /// <summary>起動時に新しいバージョンを確認する (1 日 1 回まで)</summary>
    public bool CheckUpdates { get; set; } = true;
    public DateTime? LastUpdateCheck { get; set; }
    /// <summary>「このバージョンはスキップ」したバージョン</summary>
    public string? SkippedVersion { get; set; }

    public AppSettings Clone() => (AppSettings)MemberwiseClone();

    [System.Text.Json.Serialization.JsonIgnore]
    public string EffectiveSaveFolder =>
        SaveFolder is { } f && Directory.Exists(f) ? f : DownloadsFolder;

    /// <summary>ダウンロード フォルダー (場所を移動していても実際の場所を返す)</summary>
    public static string DownloadsFolder
    {
        get
        {
            var id = new Guid("374DE290-123F-4565-9164-39C4925E467B"); // FOLDERID_Downloads
            if (SHGetKnownFolderPath(id, 0, IntPtr.Zero, out var p) == 0)
            {
                try
                {
                    var s = System.Runtime.InteropServices.Marshal.PtrToStringUni(p);
                    if (!string.IsNullOrEmpty(s) && Directory.Exists(s)) return s;
                }
                finally { System.Runtime.InteropServices.Marshal.FreeCoTaskMem(p); }
            }
            var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            return Directory.Exists(fallback) ? fallback : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll")]
    static extern int SHGetKnownFolderPath([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPStruct)] Guid id, uint flags, IntPtr token, out IntPtr path);

    /// <summary>テンプレートを展開して、ファイル名として使える文字列 (拡張子 .pdf 付き) にする</summary>
    public static string ExpandFileName(string? template, int pages, DateTime now)
    {
        var name = (string.IsNullOrWhiteSpace(template) ? DefaultFileNameTemplate : template)
            .Replace("{date}", now.ToString("yyyyMMdd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{time}", now.ToString("HHmmss"), StringComparison.OrdinalIgnoreCase)
            .Replace("{pages}", pages.ToString(), StringComparison.OrdinalIgnoreCase);
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        name = name.Trim().TrimEnd('.');
        if (name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        if (name.Length == 0) name = "ClipBind";
        return name + ".pdf";
    }

    /// <summary>同名ファイルがあれば「名前 (2).pdf」のように重ならない名前にする</summary>
    public static string UniquePath(string folder, string fileName)
    {
        var path = Path.Combine(folder, fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        for (int i = 2; File.Exists(path); i++)
            path = Path.Combine(folder, $"{stem} ({i}).pdf");
        return path;
    }

    static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClipBind PDF", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { }
        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
