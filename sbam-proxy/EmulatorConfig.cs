using Newtonsoft.Json.Linq;

namespace SbamProxy;

/// <summary>
/// Reads the emulator config.json and provides entity metadata.
/// </summary>
public class EmulatorConfig
{
    private readonly string _configPath;
    private JObject? _config;
    private DateTime _lastRead;

    public EmulatorConfig(string configPath)
    {
        _configPath = configPath;
        Reload();
    }

    public void Reload()
    {
        try {
            var json = File.ReadAllText(_configPath);
            _config = JObject.Parse(json);
            _lastRead = DateTime.UtcNow;
            Console.WriteLine($"[EmulatorConfig] Loaded config from {_configPath}");
        } catch (Exception ex) {
            Console.WriteLine($"[EmulatorConfig] Error loading config: {ex.Message}");
        }
    }

    private void CheckReload()
    {
        if (DateTime.UtcNow - _lastRead > TimeSpan.FromMinutes(1))
        {
            Reload();
        }
    }

    public List<string> GetQueueNames()
    {
        CheckReload();
        var names = new List<string>();
        var namespaces = _config?["UserConfig"]?["Namespaces"] as JArray;
        if (namespaces == null) return names;

        foreach (var ns in namespaces)
        {
            var queues = ns["Queues"] as JArray;
            if (queues == null) continue;
            foreach (var q in queues)
            {
                var name = q["Name"]?.ToString();
                if (!string.IsNullOrWhiteSpace(name))
                    names.Add(name);
            }
        }
        return names;
    }

    public List<string> GetTopicNames()
    {
        CheckReload();
        var names = new List<string>();
        var namespaces = _config?["UserConfig"]?["Namespaces"] as JArray;
        if (namespaces == null) return names;

        foreach (var ns in namespaces)
        {
            var topics = ns["Topics"] as JArray;
            if (topics == null) continue;
            foreach (var t in topics)
            {
                var name = t["Name"]?.ToString();
                if (!string.IsNullOrWhiteSpace(name))
                    names.Add(name);
            }
        }
        return names;
    }

    public bool GetQueueSessionSupport(string queueName)
    {
        CheckReload();
        var namespaces = _config?["UserConfig"]?["Namespaces"] as JArray;
        if (namespaces == null) return false;

        foreach (var ns in namespaces)
        {
            var queues = ns["Queues"] as JArray;
            if (queues == null) continue;
            foreach (var q in queues)
            {
                if (string.Equals(q["Name"]?.ToString(), queueName, StringComparison.OrdinalIgnoreCase))
                {
                    return q["Properties"]?["RequiresSession"]?.Value<bool>() ?? false;
                }
            }
        }
        return false;
    }

    public List<string> GetSubscriptionNames(string topicName)
    {
        CheckReload();
        var names = new List<string>();
        var namespaces = _config?["UserConfig"]?["Namespaces"] as JArray;
        if (namespaces == null) return names;

        foreach (var ns in namespaces)
        {
            var topics = ns["Topics"] as JArray;
            if (topics == null) continue;
            foreach (var t in topics)
            {
                if (string.Equals(t["Name"]?.ToString(), topicName, StringComparison.OrdinalIgnoreCase))
                {
                    var subs = t["Subscriptions"] as JArray;
                    if (subs == null) continue;
                    foreach (var s in subs)
                    {
                        var name = s["Name"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(name))
                            names.Add(name);
                    }
                }
            }
        }
        return names;
    }

    public bool GetSubscriptionSessionSupport(string topicName, string subName)
    {
        CheckReload();
        var namespaces = _config?["UserConfig"]?["Namespaces"] as JArray;
        if (namespaces == null) return false;

        foreach (var ns in namespaces)
        {
            var topics = ns["Topics"] as JArray;
            if (topics == null) continue;
            foreach (var t in topics)
            {
                if (string.Equals(t["Name"]?.ToString(), topicName, StringComparison.OrdinalIgnoreCase))
                {
                    var subs = t["Subscriptions"] as JArray;
                    if (subs == null) continue;
                    foreach (var s in subs)
                    {
                        if (string.Equals(s["Name"]?.ToString(), subName, StringComparison.OrdinalIgnoreCase))
                        {
                            return s["Properties"]?["RequiresSession"]?.Value<bool>() ?? false;
                        }
                    }
                }
            }
        }
        return false;
    }

    public bool QueueExists(string name) =>
        GetQueueNames().Any(q => string.Equals(q, name, StringComparison.OrdinalIgnoreCase));

    public bool TopicExists(string name) =>
        GetTopicNames().Any(t => string.Equals(t, name, StringComparison.OrdinalIgnoreCase));

    public bool SubscriptionExists(string topicName, string subName) =>
        GetSubscriptionNames(topicName).Any(s => string.Equals(s, subName, StringComparison.OrdinalIgnoreCase));
}
