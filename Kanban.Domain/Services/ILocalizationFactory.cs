using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Kanban.Util;

namespace Kanban.Services;

/// <summary>
/// resx ではなく JSON リソース(Kanban.Domain/Resources/locales/{en,ja,zh}.json)から ILocalization を生成する。
/// キーは ILocalization のプロパティ名。Domain は resx に依存しない(WPF 既存の Kanban.Localization は使わない)。
/// </summary>
public interface ILocalizationFactory
{
    ILocalization Create(Languages lang);
}

public sealed class JsonLocalizationFactory : ILocalizationFactory
{
    private static readonly ConcurrentDictionary<Languages, ILocalization> s_cache = new();

    public ILocalization Create(Languages lang) =>
        s_cache.GetOrAdd(lang, static l => new JsonLocalization(Load(l)));

    private static Dictionary<string, string> Load(Languages lang)
    {
        var assembly = typeof(JsonLocalizationFactory).Assembly;
        string suffix = $"locales.{Code(lang)}.json";
        string resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"locale resource not found: {suffix}");

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"locale resource not readable: {resourceName}");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidOperationException($"invalid locale json: {resourceName}");
    }

    private static string Code(Languages lang) => lang switch
    {
        Languages.Japanese => "ja",
        Languages.Chinese => "zh",
        _ => "en",
    };

    private sealed class JsonLocalization : ILocalization
    {
        private readonly Dictionary<string, string> m_values;

        public JsonLocalization(Dictionary<string, string> values) => m_values = values;

        private string V(string key) => m_values.TryGetValue(key, out var v)
            ? v
            : throw new InvalidOperationException($"locale key missing: {key}");

        public string BoardDefaultTitle => V(nameof(BoardDefaultTitle));
        public string BoardDefaultDescription => V(nameof(BoardDefaultDescription));
        public string CardDefaultTitle => V(nameof(CardDefaultTitle));
        public string ProcessBackLogTitle => V(nameof(ProcessBackLogTitle));
        public string ProcessRequirementTitle => V(nameof(ProcessRequirementTitle));
        public string ProcessDevelopmentTitle => V(nameof(ProcessDevelopmentTitle));
        public string ProcessTestTitle => V(nameof(ProcessTestTitle));
        public string ProcessReleaseTitle => V(nameof(ProcessReleaseTitle));
        public string ProcessPreparingTitle => V(nameof(ProcessPreparingTitle));
        public string ProcessWIPTitle => V(nameof(ProcessWIPTitle));
        public string ProcessDoneTitle => V(nameof(ProcessDoneTitle));
        public string ProcessNewStepTitle => V(nameof(ProcessNewStepTitle));
    }
}
