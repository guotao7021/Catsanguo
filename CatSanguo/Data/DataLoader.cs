using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Collections.Generic;
using CatSanguo.Data.Schemas;

namespace CatSanguo.Data;

public static class DataLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static List<T> LoadList<T>(string filePath)
    {
        string json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<List<T>>(json, Options) ?? new List<T>();
    }

    public static T Load<T>(string filePath)
    {
        string json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<T>(json, Options)!;
    }

    public static string GetDataPath(string fileName)
    {
        return Path.Combine("Data", fileName);
    }
}
