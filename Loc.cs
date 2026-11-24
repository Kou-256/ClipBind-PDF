using System.Globalization;

namespace ClipboardPDF;

/// <summary>表示言語。0 = Windows に合わせる, 1 = 日本語, 2 = English</summary>
enum UiLanguage { System = 0, Japanese = 1, English = 2 }

/// <summary>日本語 / 英語の切り替え。文言は呼び出し側に両方並べて書く (L.T("日本語", "English"))</summary>
static class L
{
    public static bool Ja { get; private set; } = true;

    public static void Init(int language)
    {
        Ja = (UiLanguage)language switch
        {
            UiLanguage.Japanese => true,
            UiLanguage.English => false,
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ja",
        };
    }

    public static string T(string ja, string en) => Ja ? ja : en;
}
