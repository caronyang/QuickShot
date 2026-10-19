using System.Windows.Data;
using System.Windows.Markup;
using QuickShot.Services;

namespace QuickShot.Resources;

/// <summary>
/// XAML helper for localised text:
/// <c>&lt;TextBlock Text="{res:Loc Settings_Title}" /&gt;</c>.
/// Binds to <see cref="LocalizationService"/>'s indexer, so the UI re-translates live
/// when the language is switched (the keys intentionally avoid '.' because the binding
/// path parser would treat them as property separators).
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = LocalizationService.Instance,
            Mode = BindingMode.OneWay,
            FallbackValue = Key,
            TargetNullValue = Key,
        };

        return binding.ProvideValue(serviceProvider);
    }
}
