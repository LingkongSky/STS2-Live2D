using STS2RitsuLib;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils;

namespace Live2D.Scripts.UI;

public static class Live2DLocalization
{
    private static I18N? _instance;

    public static I18N Instance => _instance ??= RitsuLibFramework.CreateModLocalizationWithFallback(
        modId: Entry.ModId,
        instanceName: $"{Entry.ModId}.settings",
        resourceFolders: ["Live2D.Localization"],
        resourceAssembly: typeof(Live2DLocalization).Assembly,
        fallbackLanguage: "eng");

    public static string Get(string key, string fallback) => Instance.Get(key, fallback);

    public static string Format(string key, string fallback, params object?[] args)
        => string.Format(Get(key, fallback), args);

    public static ModSettingsText Text(string key, string fallback)
        => ModSettingsText.I18N(Instance, key, fallback);
}
