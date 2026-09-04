using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HttpProxyWpfClient.code.net;

/// <summary>
/// 负责将 <see cref="AppConfig"/> 读写到 exe 同目录下的 config.json
/// </summary>
public static class ConfigService
{
    private static readonly string ConfigPath =
        Path.Combine(AppContext.BaseDirectory, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly object FileLock = new object();

    /// <summary>
    /// 从 config.json 加载配置；文件不存在或解析失败时返回默认配置
    /// </summary>
    public static AppConfig Load()
    {
        try
        {
            lock (FileLock)
            {
                if (!File.Exists(ConfigPath))
                {
                    return new AppConfig();
                }

                string json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                return config ?? new AppConfig();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载配置文件失败，使用默认配置: {ex.Message}");
            return new AppConfig();
        }
    }

    /// <summary>
    /// 将配置写入 config.json（覆盖）
    /// </summary>
    public static void Save(AppConfig config)
    {
        try
        {
            lock (FileLock)
            {
                string json = JsonSerializer.Serialize(config, JsonOptions);
                File.WriteAllText(ConfigPath, json);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存配置文件失败: {ex.Message}");
        }
    }
}
