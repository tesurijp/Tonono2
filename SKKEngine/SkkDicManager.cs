using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Tonono2.SKKEngine;

public class SkkDicManager
{
    private readonly Dictionary<string, List<string>> mainDictionary = [];
    private readonly Dictionary<string, List<string>> userDictionary = [];
    private readonly string userDictionaryPath;

    public SkkDicManager(IEnumerable<string> mainDictPaths, string userDictPath)
    {
        Reload(mainDictPaths);
        userDictionaryPath = userDictPath;
        LoadUserDictionary(userDictionaryPath);
    }
    public void Reload(IEnumerable<string> mainDictPaths)
    {
        mainDictionary.Clear();
        foreach (var path in mainDictPaths)
        {
            LoadMainDictionary(path);
        }
    }

    private static byte[] DicBuffer(string path)
    {
        using var fileStream = File.OpenRead(path);
        using Stream inputStream = path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
            ? new GZipStream(fileStream, CompressionMode.Decompress)
            : fileStream;

        // Read into memory to handle encoding detection
        using var memoryStream = new MemoryStream();
        inputStream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    private void LoadMainDictionary(string path)
    {
        if (File.Exists(path))
        {
            try
            {
                var buffer = DicBuffer(path);
                var encoding = DetectEncoding(buffer);
                using var reader = new StreamReader(new MemoryStream(buffer), encoding);

                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    ParseLine(line, mainDictionary);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Error loading main dictionary {path}: {ex.Message}");
            }
        }
    }

    private static Encoding DetectEncoding(byte[] buffer)
    {
        // Simple heuristic for SKK dictionaries: usually EUC-JP or UTF-8.
        // If it has UTF-8 BOM, it's UTF-8.
        if (buffer.Length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
        {
            return Encoding.UTF8;
        }

        // Try to see if it's valid UTF-8
        try
        {
            var utf8 = new UTF8Encoding(false, true);
            utf8.GetString(buffer);
            return utf8;
        }
        catch (ArgumentException)
        {
            // Fallback to EUC-JP
            return Encoding.GetEncoding("euc-jp");
        }
    }

    private void LoadUserDictionary(string path)
    {
        if (File.Exists(path))
        {
            foreach (var line in File.ReadLines(path, Encoding.UTF8))
            {
                ParseLine(line, userDictionary);
            }
        }
    }

    private static void ParseLine(string line, Dictionary<string, List<string>> targetDict)
    {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith(';'))
        {
            return;
        }

        var spaceIndex = line.IndexOf(' ');
        if (spaceIndex < 0)
        {
            return;
        }

        var reading = line[..spaceIndex];
        var candidatesPart = line[spaceIndex..].Trim();

        if (candidatesPart.StartsWith('/') && candidatesPart.EndsWith('/'))
        {
            var candidates = candidatesPart
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Split(';').First());

            if (targetDict.TryGetValue(reading, out var prev))
            {
                candidates = [.. prev.Union(candidates)];
            }
            targetDict[reading] = [.. candidates];
        }
    }

    public IEnumerable<string> GetCandidates(string reading)
    {
        var cand1 = userDictionary.TryGetValue(reading, out var val1) ? val1 : [];
        var cand2 = mainDictionary.TryGetValue(reading, out var val2) ? val2 : [];
        return cand1.Union(cand2);
    }

    public void AddWord(string reading, string word)
    {
        if (userDictionary.TryGetValue(reading, out var candidates))
        {
            candidates.Remove(word);
            candidates.Insert(0, word);
        }
        else
        {
            userDictionary[reading] = [word];
        }
        SaveUserDictionary();
    }

    public void RemoveWord(string reading, string word)
    {
        if (userDictionary.TryGetValue(reading, out var candidates))
        {
            if (candidates.Remove(word))
            {
                SaveUserDictionary();
            }
        }
    }

    private void SaveUserDictionary()
    {
        try
        {
            DebugLogger.Log($"Saving user dictionary to: {userDictionaryPath}");
            var lines = userDictionary.Select(kvp => $"{kvp.Key} /{string.Join("/", kvp.Value)}/").ToList();
            File.WriteAllLines(userDictionaryPath, lines, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"Failed to save user dictionary: {ex.Message}");
        }
    }
}
