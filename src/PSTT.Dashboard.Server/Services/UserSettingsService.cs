using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PSTT.Dashboard.Server.Services;

/// <summary>
/// Thread-safe service for reading and writing to appsettings.user.json.
/// All controllers that mutate user settings should go through this service.
/// </summary>
public class UserSettingsService
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly IConfiguration _configuration;

    public UserSettingsService(DashboardStorageService storage, IConfiguration configuration)
    {
        _filePath = Path.Combine(storage.StoragePath, "appsettings.user.json");
        _configuration = configuration;
    }

    /// <summary>Updates the user settings file and reloads IConfiguration.</summary>
    public async Task UpdateAsync(Action<JsonObject> mutate)
    {
        await _lock.WaitAsync();
        try
        {
            var root = Read();
            mutate(root);
            Write(root);
        }
        finally
        {
            _lock.Release();
        }

        if (_configuration is IConfigurationRoot configRoot)
            configRoot.Reload();
    }

    /// <summary>Reads a value from the current settings file (outside a lock — use for one-off reads).</summary>
    public T? Get<T>(string key, T? defaultValue = default)
    {
        try
        {
            var root = Read();
            // Navigate dotted key (e.g. "Auth:AdminPasswordHash")
            JsonNode? node = root;
            foreach (var segment in key.Split(':'))
            {
                node = node?[segment];
                if (node == null) return defaultValue;
            }
            return node.GetValue<T>();
        }
        catch
        {
            return defaultValue;
        }
    }

    private JsonObject Read()
    {
        if (File.Exists(_filePath))
        {
            try { return JsonNode.Parse(File.ReadAllText(_filePath))?.AsObject() ?? new JsonObject(); }
            catch { }
        }
        return new JsonObject();
    }

    private void Write(JsonObject root)
    {
        File.WriteAllText(_filePath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }
}
