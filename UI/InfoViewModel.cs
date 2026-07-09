using System.Collections.Generic;
using System.Linq;

namespace Tonono2.UI;

public class InfoViewModel(AppConfig cfg)
{
    public static string ConfigPath => AppConfig.ConfigPath;
    public IEnumerable<KeyValuePair<string, string>> RomajiEntries => cfg.RomajiTable.OrderBy(kv => kv.Key);
    public IEnumerable<KeyValuePair<char, string>> ZenkakuEntries => cfg.ZenkakuTable.OrderBy(kv => kv.Key);
    public IEnumerable<KeyValuePair<string, string>> MoraModifierEntries => cfg.MoraModifier.OrderBy(kv => kv.Key);
    public IEnumerable<KeyValuePair<string, string>> MoraAutoCompleteEntries => cfg.MoraAutoComplete.OrderBy(kv => kv.Key);
    public IEnumerable<string> DictionaryPaths => cfg.DictionaryPaths;
    public IEnumerable<string> ViCompatibleApps => cfg.ViCompatibleApps;
}
