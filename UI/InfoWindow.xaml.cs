using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Tonono2.SKKEngine;

namespace Tonono2.UI;

public class InfoViewModel(AppConfig cfg)
{
    public static string ConfigPath => AppConfig.ConfigPath;
    public IEnumerable<KeyValuePair<string, string>> RomajiEntries => cfg.RomajiTable.OrderBy(kv => kv.Key);
    public IEnumerable<KeyValuePair<string, string>> ZenkakuEntries => cfg.ZenkakuTable.OrderBy(kv => kv.Key);
    public IEnumerable<string> DictionaryPaths => cfg.DictionaryPaths;
    public IEnumerable<string> ViCompatibleApps => cfg.ViCompatibleApps;
}

public partial class InfoWindow : Window
{
    public InfoWindow(AppConfig cfg)
    {
        InitializeComponent();
        DataContext = new InfoViewModel(cfg);
    }
}
