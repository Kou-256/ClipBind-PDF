namespace ClipboardPDF;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        L.Init(AppSettings.Load().Language);
        using var iconStream = typeof(Program).Assembly.GetManifestResourceStream("ClipBind.ico")!;
        using var icon = new Icon(iconStream);
        using var form = new MainForm { Icon = icon };
        Application.Run(form);
    }
}
