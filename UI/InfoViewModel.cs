using System.Collections.Generic;
using System.Linq;

namespace Tonono2.UI;

public sealed class InfoStringPairRow
{
    public required string Key { get; init; }
    public required string Value { get; init; }
}

public sealed class InfoCharStringPairRow
{
    public required char Key { get; init; }
    public required string Value { get; init; }
}

public class InfoViewModel(AppConfig cfg)
{
    public static string ConfigPath => AppConfig.ConfigPath;
    public IEnumerable<InfoStringPairRow> RomajiEntries => cfg.RomajiTable
        .OrderBy(kv => kv.Key)
        .Select(kv => new InfoStringPairRow { Key = kv.Key, Value = kv.Value });
    public IEnumerable<InfoCharStringPairRow> ZenkakuEntries => cfg.ZenkakuTable
        .OrderBy(kv => kv.Key)
        .Select(kv => new InfoCharStringPairRow { Key = kv.Key, Value = kv.Value });
    public IEnumerable<InfoStringPairRow> MoraModifierEntries => cfg.MoraModifier
        .OrderBy(kv => kv.Key)
        .Select(kv => new InfoStringPairRow { Key = kv.Key, Value = kv.Value });
    public IEnumerable<InfoStringPairRow> MoraAutoCompleteEntries => cfg.MoraAutoComplete
        .OrderBy(kv => kv.Key)
        .Select(kv => new InfoStringPairRow { Key = kv.Key, Value = kv.Value });
    public IEnumerable<string> DictionaryPaths => cfg.DictionaryPaths;
    public IEnumerable<string> ViCompatibleApps => cfg.ViCompatibleApps;
}
