using System;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;

namespace KztekComponentAvalonia;

public static class KztekThemeExtension
{
    public static AppBuilder UseKztekTheme(this AppBuilder builder)
    {
        return builder.AfterSetup(_ =>
        {
            if (Application.Current is not { } app) return;

            var include = new ResourceInclude(new Uri("avares://KztekComponentAvalonia/"))
            {
                Source = new Uri("avares://KztekComponentAvalonia/Themes/Default.axaml")
            };
            app.Resources.MergedDictionaries.Add(include);
        });
    }
}
