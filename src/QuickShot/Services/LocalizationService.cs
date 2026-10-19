using System.ComponentModel;
using System.Globalization;
using System.Windows;

namespace QuickShot.Services;

/// <summary>
/// Runtime language switching. Strings live in Resources\Strings.&lt;code&gt;.xaml and are
/// looked up through the indexer, so XAML bindings refresh the moment the language changes.
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    public const string DefaultLanguage = "en";

    public static readonly (string Code, string NativeName)[] Supported =
    {
        ("en", "English"),
        ("zh-TW", "繁體中文"),
    };

    private ResourceDictionary? _dictionary;

    private LocalizationService()
    {
    }

    public static LocalizationService Instance { get; } = new();

    public string CurrentLanguage { get; private set; } = DefaultLanguage;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Localised string for <paramref name="key"/>; the key itself when missing.</summary>
    public string this[string key] =>
        _dictionary is not null && _dictionary[key] is string value ? value : key;

    public string Format(string key, params object?[] args)
    {
        try
        {
            return string.Format(CultureInfo.CurrentCulture, this[key], args);
        }
        catch (FormatException)
        {
            return this[key];
        }
    }

    public static bool IsSupported(string? code) =>
        code is not null && Supported.Any(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));

    public void Initialize(string? language) => Apply(IsSupported(language) ? language! : DefaultLanguage, notify: false);

    public void SetLanguage(string code)
    {
        if (!IsSupported(code)) return;
        if (string.Equals(code, CurrentLanguage, StringComparison.OrdinalIgnoreCase)) return;
        Apply(code, notify: true);
    }

    private void Apply(string code, bool notify)
    {
        // Note: the file name must not look like "name.culture.ext" or MSBuild would move
        // the compiled BAML into a satellite assembly and the pack URI below would miss it.
        var dictionary = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Resources/Strings_{code}.xaml", UriKind.Absolute),
        };

        var merged = Application.Current?.Resources.MergedDictionaries;
        merged?.Add(dictionary);
        if (_dictionary is not null) merged?.Remove(_dictionary);
        _dictionary = dictionary;

        CurrentLanguage = code;
        try
        {
            var culture = new CultureInfo(code);
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }
        catch (CultureNotFoundException)
        {
            // Unsupported on this machine - strings still work, only formatting culture stays.
        }

        if (notify) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
}
