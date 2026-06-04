using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using VYaml.Annotations;
using VYaml.Serialization;
using System.Text;
using System.Threading;

namespace Tonono2;

public record class AppConfig(
    Dictionary<string, string> RomajiTable, Dictionary<string, string> MoraModifier, Dictionary<string, string> MoraAutoComplete,
    Dictionary<char, string> ZenkakuTable,
    string[] DictionaryPaths, string UserDictionaryPath,
    string[] ViCompatibleApps)
{
    public bool HasError => Enumerable.Any([RomajiTable.Count, MoraModifier.Count, ZenkakuTable.Count, DictionaryPaths.Length], i => i < 1);
    public bool HasChange(AppConfig other) => !(
        UserDictionaryPath == other.UserDictionaryPath &&
        RomajiTable.SequenceEqual(other.RomajiTable) &&
        ZenkakuTable.SequenceEqual(other.ZenkakuTable) &&
        MoraModifier.SequenceEqual(other.MoraModifier) &&
        MoraAutoComplete.SequenceEqual(other.MoraAutoComplete) &&
        DictionaryPaths.SequenceEqual(other.DictionaryPaths) &&
        ViCompatibleApps.SequenceEqual(other.ViCompatibleApps)
        );
#if DEBUG
    public static bool HasUserConfig => false;
#else
    public static bool HasUserConfig => File.Exists(UserConfigPath);
#endif
    public const string ConfigFileName = "config.yaml";
    public static string ConfigPath => HasUserConfig ? UserConfigPath : SystemConfigPath;
    public static string ConfigFolder => HasUserConfig ? UserConfigFolder : SystemConfigFolder;
    public static readonly string SystemConfigFolder = AppContext.BaseDirectory;
    public static readonly string SystemConfigPath = Path.Combine(SystemConfigFolder, ConfigFileName);
    public static readonly string UserConfigFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tonono2");
    public static readonly string UserConfigPath = Path.Combine(UserConfigFolder, ConfigFileName);
}

[YamlObject] public partial record class RomajiTable(string Vowel, Dictionary<string, string[]> Rows, Dictionary<string, string> Irregular, Dictionary<string, List<string>> MoraModifier, Dictionary<string,string> MoraAutoComplete);
[YamlObject] public partial record class Standard(char Start, char End,int Offset);
[YamlObject] public partial record class ZenkakuTable(Standard Standard, Dictionary<string, string> Irregular);
[YamlObject] public partial record class ConfigYaml(string[] DictionaryPaths, string UserDictionaryPath, RomajiTable RomajiTable, ZenkakuTable ZenkakuTable, string[] ViCompatibleApps);

public static class ConfigLoader
{
    public static Action<AppConfig>? UpdateConfig { get; set; }
    public static AppConfig CurrentConfig { get; private set; } = new([], [], [], [], [], "", []);

    private static readonly FileSystemWatcher systemConfigWatcher = StartWatcher(AppConfig.SystemConfigFolder);
    private static readonly FileSystemWatcher userConfigWatcher = StartWatcher(AppConfig.UserConfigFolder);

    private static FileSystemWatcher StartWatcher(string folderpath)
    {
        if (!Directory.Exists(folderpath))
        {
            Directory.CreateDirectory(folderpath);
        }
        var watcher = new FileSystemWatcher(folderpath, AppConfig.ConfigFileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            EnableRaisingEvents = true,
            IncludeSubdirectories = false,
            
        };
        watcher.Created += (_, _) => OnConfigChanged();
        watcher.Changed += (_, _) => OnConfigChanged();
        watcher.Renamed += (_, _) => OnConfigChanged();
        watcher.Deleted += (_, _) => OnConfigChanged();
        return watcher;
    }

    public static AppConfig Reload()
    {
        DebugLogger.Log($"Loading config from: {AppConfig.ConfigPath}");
        try
        {
            var yaml = File.ReadAllText(AppConfig.ConfigPath);
            var yamlObj = YamlSerializer.Deserialize<ConfigYaml>(Encoding.UTF8.GetBytes(yaml));
            var (romaji, mora, moraComp) = LoadRomajiTable(yamlObj);
            var zenkaku = LoadZenkakuTable(yamlObj);
            var (dics, userdic) = LoadDictionaryPath(yamlObj);
            var vicompatible = LoadViCompatibleApps(yamlObj);

            var appConfig = new AppConfig(romaji, mora, moraComp, zenkaku, dics, userdic, vicompatible);
            if (appConfig.HasError)
            {
                throw new FileFormatException("Error loading config.yaml");
            }
            return appConfig;
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"Error loading config.yaml: {ex.Message}");
            throw;
        }
    }

    public static void StartWatch(Action<AppConfig> updateConfig) => UpdateConfig = updateConfig;

    public static AppConfig Load() => CurrentConfig = Reload();

    public static void Tidy()
    {
        systemConfigWatcher.Dispose();
        userConfigWatcher.Dispose();
    }

    private static void OnConfigChanged()
    {
        DebugLogger.Log("config.yaml change detected.");
        var oldConfig = CurrentConfig;
        Thread.Sleep(500);
        try
        {
            CurrentConfig = Reload();
            if (oldConfig.HasChange(CurrentConfig))
            {
                UpdateConfig?.Invoke(CurrentConfig);
                DebugLogger.Log("config.yaml update success.");
            }
        }
        catch { }
    }
    private static string PathConvert(string path) => path.Length > 0 && path[0] == '.' ? Path.Combine(AppConfig.ConfigFolder, path) : Path.GetFullPath(path);

    private static (string[] systemDic, string userDic) LoadDictionaryPath(ConfigYaml data) =>
        ([.. data.DictionaryPaths.Select(PathConvert)], PathConvert(data.UserDictionaryPath));

    private static Dictionary<char, string> LoadZenkakuTable(ConfigYaml data)
    {
        var startVal = data.ZenkakuTable.Standard.Start;
        var endVal = data.ZenkakuTable.Standard.End;
        var offset = data.ZenkakuTable.Standard.Offset;

        var zenkaku = Enumerable.Sequence(startVal, endVal, (char)1).Select(i => (i, ((char)(i + offset)).ToString())).ToDictionary();
        foreach (var (key, value) in  data.ZenkakuTable.Irregular)
        {
            zenkaku[key[0]] = value;
        }
        return zenkaku;
    }

    private static (Dictionary<string, string> romaji, Dictionary<string, string> mora, Dictionary<string, string> moraCompete) LoadRomajiTable(ConfigYaml data)
    {
        var vowels = data.RomajiTable.Vowel;
        var rows = data.RomajiTable.Rows;

        var romaji = rows.SelectMany(row => vowels.Select((vowel, i) => (key: row.Key + vowel, Kana: row.Value[i])))
            .Where(x => !string.IsNullOrEmpty(x.Kana)).ToDictionary();
        foreach (var (key, value) in  data.RomajiTable.Irregular)
        {
            romaji[key] = value;
        }

        var mora = data.RomajiTable.MoraModifier.SelectMany(k => k.Value.Select(item => (item, ch: k.Key))).ToDictionary();
        return (romaji, mora, data.RomajiTable.MoraAutoComplete);
    }

    private static string[] LoadViCompatibleApps(ConfigYaml data) => [.. data.ViCompatibleApps.Select(i => i.Replace('/', '\\'))];
}
