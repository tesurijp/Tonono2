using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Tonono2.SKKEngine;

namespace Tonono2.UI;

public class InfoViewModel(SkkController controller)
{
    public static string ConfigPath => AppConfig.ConfigPath;
    public IEnumerable<KeyValuePair<string, string>> RomajiEntries => controller.Config.RomajiTable.OrderBy(kv => kv.Key);
    public IEnumerable<KeyValuePair<string, string>> ZenkakuEntries => controller.Config.ZenkakuTable.OrderBy(kv => kv.Key);
    public IEnumerable<string> DictionaryPaths => controller.Config.DictionaryPaths;
}

public partial class InfoWindow : Window
{
    public InfoWindow(SkkController controller)
    {
        InitializeComponent();
        DataContext = new InfoViewModel(controller);
    }
}
