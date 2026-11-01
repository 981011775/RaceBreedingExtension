#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;

namespace ComplexBreedingRedux.Utilities;

/// <summary>从嵌入为 EmbeddedResource 的 CSV 文件加载数据。</summary>
internal static class CsvLoader
{
    /// <summary>加载包含两列（键，值）的 CSV 到 Dictionary&lt;int, int&gt;。</summary>
    /// <param name="resourcePath">嵌入资源的路径（例如 "ComplexBreedingRedux.Features.SlaveMarket.Data.file.csv"）。</param>
    public static Dictionary<int, int> LoadIntIntDictionary(string resourcePath)
    {
        var result = new Dictionary<int, int>();
        string? csvContent = GetEmbeddedResource(resourcePath);

        if (string.IsNullOrWhiteSpace(csvContent))
            return result;

        var lines = csvContent!.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines.Skip(1)) // 跳过标题行
        {
            var parts = line.Split(',');
            if (parts.Length >= 2 &&
                int.TryParse(parts[0].Trim(), out int key) &&
                int.TryParse(parts[1].Trim(), out int value))
            {
                result[key] = value;
            }
        }

        return result;
    }

    /// <summary>从 CSV 加载 float 值（标题行后的第一行，第二列）。</summary>
    /// <param name="resourcePath">嵌入资源的路径。</param>
    /// <param name="defaultValue">加载失败时的默认值。</param>
    public static float LoadFloatValue(string resourcePath, float defaultValue = 0f)
    {
        string? csvContent = GetEmbeddedResource(resourcePath);

        if (string.IsNullOrWhiteSpace(csvContent))
            return defaultValue;

        var lines = csvContent!.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length < 2)
            return defaultValue;

        var parts = lines[1].Split(',');
        if (parts.Length >= 2 && float.TryParse(parts[1].Trim(), out float value))
            return value;

        return defaultValue;
    }

    /// <summary>
    /// 获取嵌入资源的内容作为字符串。
    /// </summary>
    /// <param name="resourcePath">嵌入资源的路径（例如 "ComplexBreedingRedux.Features.SlaveMarket.Data.file.csv"）。</param>
    /// <returns>资源的内容，如果未找到则返回 null。</returns>
    private static string? GetEmbeddedResource(string resourcePath)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourcePath);
        if (stream == null)
            return null;

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}