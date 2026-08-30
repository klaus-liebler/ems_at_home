using System.Text.Json;

namespace EmsAtHome.Eebus;

/// <summary>Flat-file JSON-backed <see cref="ITrustedSkiStore"/> - see interface doc comment for why.</summary>
public sealed class JsonTrustedSkiStore : ITrustedSkiStore
{
    private readonly string _path;
    private readonly object _lock = new();

    public JsonTrustedSkiStore(EebusOptions options)
    {
        _path = Path.Combine(options.BasePath, "trusted-skis.json");
    }

    public IReadOnlyCollection<string> GetTrustedSkis()
    {
        lock (_lock)
        {
            if (!File.Exists(_path))
                return [];

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
    }

    public void AddTrustedSki(string ski)
    {
        lock (_lock)
        {
            var skis = new HashSet<string>(GetTrustedSkis()) { ski };
            Save(skis);
        }
    }

    public void RemoveTrustedSki(string ski)
    {
        lock (_lock)
        {
            var skis = new HashSet<string>(GetTrustedSkis());
            skis.Remove(ski);
            Save(skis);
        }
    }

    private void Save(IEnumerable<string> skis) =>
        File.WriteAllText(_path, JsonSerializer.Serialize(skis.ToList()));
}
