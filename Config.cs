using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using VYaml.Annotations;
using VYaml.Serialization;
using System.Text;

namespace Tonono2;

public class AppConfig
{
    public Dictionary<string, string> RomajiTable { get; } = [];
    public Dictionary<string, string> MoraModifier  { get; } = [];
    public Dictionary<string, string> ZenkakuTable { get; } = [];
    public List<string> DictionaryPaths { get; set; } = [];
    public string UserDictionaryPath { get; set; } = "";
    public List<string> ViCompatibleApps { get; set; } = [];
    public static string ConfigPath => Path.Combine(AppContext.BaseDirectory, "config.yaml");
}

[YamlObject] public partial record class RomajiTable(string Vowel, Dictionary<string, string[]> Rows, Dictionary<string, string> Irregular, Dictionary<string, List<string>> MoraModifier);
[YamlObject] public partial record class Standard(int Start, int End, int Offset);
[YamlObject] public partial record class ZenkakuTable(Standard Standard, Dictionary<string, string> Overrides);
[YamlObject] public partial record class ConfigYaml(string[] DictionaryPaths, string UserDictionaryPath, RomajiTable RomajiTable, ZenkakuTable ZenkakuTable, string[] ViCompatibleApps);

public static class ConfigLoader
{
    public static AppConfig Load(string yaml)
    {
        var yamlObj = YamlSerializer.Deserialize<ConfigYaml>(Encoding.UTF8.GetBytes(yaml));

        var appConfig = new AppConfig();

        LoadRomajiTable(yamlObj, appConfig);
        LoadZenkakuTable(yamlObj, appConfig);
        LoadDictionaryPath(yamlObj, appConfig);
        LoadViCompatibleApps(yamlObj, appConfig);

        return appConfig;
    }

    private static void LoadDictionaryPath(ConfigYaml data, AppConfig appConfig)
    {
        appConfig.DictionaryPaths = [.. data.DictionaryPaths.Select(p => Path.Combine(AppContext.BaseDirectory, p))];
        appConfig.UserDictionaryPath = Path.Combine(AppContext.BaseDirectory, data.UserDictionaryPath);
    }

    private static void LoadZenkakuTable(ConfigYaml data, AppConfig appConfig)
    {
        var startVal = data.ZenkakuTable.Standard.Start;
        var endVal = data.ZenkakuTable.Standard.End;
        var offset = data.ZenkakuTable.Standard.Offset;

        for (var i = startVal; i <= endVal; i++)
        {
            appConfig.ZenkakuTable[((char)i).ToString()] = ((char)(i + offset)).ToString();
        }

        var overrides = data.ZenkakuTable.Overrides;
        foreach (var entry in overrides)
        {
            appConfig.ZenkakuTable[entry.Key.ToString() ?? ""] = entry.Value?.ToString() ?? "";
        }
    }

    private static void LoadRomajiTable(ConfigYaml data, AppConfig appConfig)
    {
        var vowels = data.RomajiTable.Vowel;
        var rows = data.RomajiTable.Rows;

        foreach (var row in rows)
        {
            var prefix = row.Key?.ToString() ?? "";
            var kanaList = row.Value;

            for (var i = 0; i < vowels.Length && i < kanaList.Length; i++)
            {
                var kana = kanaList[i]?.ToString();
                if (!string.IsNullOrEmpty(kana))
                {
                    var key = prefix + vowels[i];
                    appConfig.RomajiTable[key] = kana;
                }
            }
        }

        var irregulars = data.RomajiTable.Irregular;
        foreach (var entry in irregulars)
        {
            appConfig.RomajiTable[entry.Key?.ToString() ?? ""] = entry.Value?.ToString() ?? "";
        }

        appConfig.MoraModifier.Clear();
        foreach (var (ch, list) in data.RomajiTable.MoraModifier)
        {
            foreach (var item in list)
            {
                appConfig.MoraModifier[item] = ch;
            }
        }
    }

    private static void LoadViCompatibleApps(ConfigYaml data, AppConfig appConfig) =>
        appConfig.ViCompatibleApps = [.. data.ViCompatibleApps.Select(i => i.Replace('/', '\\'))];
}
