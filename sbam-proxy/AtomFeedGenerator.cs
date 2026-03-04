using System.Xml.Linq;

namespace SbamProxy;

/// <summary>
/// Generates Atom XML responses that match the Azure Service Bus management REST API format.
/// The NamespaceManager from the legacy WindowsAzure.ServiceBus SDK expects this exact format.
/// </summary>
public static class AtomFeedGenerator
{
    // XML namespaces used by the Service Bus REST API
    private static readonly XNamespace AtomNs = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace SbNs = "http://schemas.microsoft.com/netservices/2010/10/servicebus/connect";
    private static readonly XNamespace CountNs = "http://schemas.microsoft.com/netservices/2011/06/servicebus";
    private static readonly XNamespace XsiNs = "http://www.w3.org/2001/XMLSchema-instance";

    private const string BaseUrl = "https://localhost";
    private const string DefaultTtl = "P10675199DT2H48M5.4775807S";
    private const string DefaultLockDuration = "PT1M";
    private const string DefaultDuplicateWindow = "PT10M";
    private static readonly string Now = DateTime.UtcNow.ToString("o");

    // --- Atom Feed (list) responses ---

    public static string GenerateQueuesFeed(List<string> queueNames)
    {
        var entries = queueNames.Select(name => CreateQueueEntry(name, 0, 0, false));
        return CreateFeed("Queues", "$Resources/Queues", entries);
    }

    public static async Task<string> GenerateQueuesFeedWithCounts(List<string> queueNames, Func<string, Task<(long active, long dlq, bool sessions)>> fetcher)
    {
        var entries = new List<XElement>();
        foreach(var name in queueNames) {
            var result = await fetcher(name);
            entries.Add(CreateQueueEntry(name, result.active, result.dlq, result.sessions));
        }
        return CreateFeed("Queues", "$Resources/Queues", entries);
    }

    public static string GenerateTopicsFeed(List<string> topicNames)
    {
        var entries = topicNames.Select(name => CreateTopicEntry(name));
        return CreateFeed("Topics", "$Resources/Topics", entries);
    }

    public static string GenerateSubscriptionsFeed(string topicPath, List<string> subNames, Func<string, bool> sessionChecker)
    {
        var entries = subNames.Select(name => CreateSubscriptionEntry(topicPath, name, 0, 0, sessionChecker(name)));
        return CreateFeed("Subscriptions", $"{topicPath}/Subscriptions", entries);
    }

    public static async Task<string> GenerateSubscriptionsFeedWithCounts(string topicPath, List<string> subNames, Func<string, Task<(long active, long dlq, bool sessions)>> fetcher)
    {
        var entries = new List<XElement>();
        foreach(var name in subNames) {
            var result = await fetcher(name);
            entries.Add(CreateSubscriptionEntry(topicPath, name, result.active, result.dlq, result.sessions));
        }
        return CreateFeed("Subscriptions", $"{topicPath}/Subscriptions", entries);
    }

    public static string GenerateRulesFeed(string topicPath, string subName)
    {
        var entry = CreateRuleEntry(topicPath, subName, "$Default");
        return CreateFeed("Rules", $"{topicPath}/Subscriptions/{subName}/Rules", new[] { entry });
    }

    public static string GenerateEmptyFeed(string title, string path)
    {
        return CreateFeed(title, path, Enumerable.Empty<XElement>());
    }

    // --- Atom Entry (single entity) responses ---

    public static string GenerateQueueEntry(string name, long active, long dlq, bool requiresSession)
    {
        return WrapEntry(CreateQueueEntry(name, active, dlq, requiresSession));
    }

    public static string GenerateTopicEntry(string name)
    {
        return WrapEntry(CreateTopicEntry(name));
    }

    public static string GenerateSubscriptionEntry(string topicPath, string subName, long active, long dlq, bool requiresSession)
    {
        return WrapEntry(CreateSubscriptionEntry(topicPath, subName, active, dlq, requiresSession));
    }

    public static string GenerateNamespaceInfoEntry()
    {
        // Format matched exactly from a real Azure Service Bus response
        var nsInfo = new XElement(SbNs + "NamespaceInfo",
            new XAttribute(XNamespace.Xmlns + "i", XsiNs),
            new XElement(SbNs + "CreatedTime", "2024-03-04T00:00:00.000Z"),
            new XElement(SbNs + "MessagingSKU", "Standard"),
            new XElement(SbNs + "ModifiedTime", "2024-03-04T00:00:00.000Z"),
            new XElement(SbNs + "Name", "localhost"),
            new XElement(SbNs + "NamespaceType", "Messaging")
        );

        var entry = new XElement(AtomNs + "entry",
            new XElement(AtomNs + "id", $"{BaseUrl}/$namespaceinfo?api-version=2021-05&enrich=False"),
            new XElement(AtomNs + "title", new XAttribute("type", "text"), "localhost"),
            new XElement(AtomNs + "updated", Now),
            new XElement(AtomNs + "author",
                new XElement(AtomNs + "name", "localhost")
            ),
            new XElement(AtomNs + "link",
                new XAttribute("rel", "self"),
                new XAttribute("href", $"{BaseUrl}/$namespaceinfo?api-version=2021-05&enrich=False")
            ),
            new XElement(AtomNs + "content",
                new XAttribute("type", "application/xml"),
                nsInfo
            )
        );

        return WrapEntry(entry);
    }

    // --- Internal helpers ---

    private static string CreateFeed(string title, string path, IEnumerable<XElement> entries)
    {
        var feed = new XElement(AtomNs + "feed",
            new XElement(AtomNs + "title", new XAttribute("type", "text"), title),
            new XElement(AtomNs + "id", $"{BaseUrl}/{path}"),
            new XElement(AtomNs + "updated", Now)
        );

        foreach (var entry in entries)
        {
            feed.Add(entry);
        }

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), feed);
        return doc.Declaration + "\n" + doc.Root!.ToString();
    }

    private static string WrapEntry(XElement entry)
    {
        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), entry);
        return doc.Declaration + "\n" + doc.Root!.ToString();
    }

    private static XElement CreateQueueEntry(string name, long active, long dlq, bool requiresSession)
    {
        var description = new XElement(SbNs + "QueueDescription",
            new XElement(SbNs + "LockDuration", DefaultLockDuration),
            new XElement(SbNs + "MaxSizeInMegabytes", "1024"),
            new XElement(SbNs + "RequiresDuplicateDetection", "false"),
            new XElement(SbNs + "RequiresSession", requiresSession.ToString().ToLower()),
            new XElement(SbNs + "DefaultMessageTimeToLive", DefaultTtl),
            new XElement(SbNs + "DeadLetteringOnMessageExpiration", "false"),
            new XElement(SbNs + "DuplicateDetectionHistoryTimeWindow", DefaultDuplicateWindow),
            new XElement(SbNs + "MaxDeliveryCount", "10"),
            new XElement(SbNs + "EnableBatchedOperations", "true"),
            new XElement(SbNs + "SizeInBytes", "0"),
            new XElement(SbNs + "MessageCount", (active + dlq).ToString()),
            new XElement(SbNs + "IsAnonymousAccessible", "false"),
            new XElement(SbNs + "Status", "Active"),
            new XElement(SbNs + "SupportOrdering", "true"),
            CreateCountDetails(active, dlq),
            new XElement(SbNs + "EnablePartitioning", "false")
        );

        return CreateEntryElement(name, name, description);
    }

    private static XElement CreateTopicEntry(string name)
    {
        var description = new XElement(SbNs + "TopicDescription",
            new XElement(SbNs + "DefaultMessageTimeToLive", DefaultTtl),
            new XElement(SbNs + "MaxSizeInMegabytes", "1024"),
            new XElement(SbNs + "RequiresDuplicateDetection", "false"),
            new XElement(SbNs + "DuplicateDetectionHistoryTimeWindow", DefaultDuplicateWindow),
            new XElement(SbNs + "EnableBatchedOperations", "true"),
            new XElement(SbNs + "SizeInBytes", "0"),
            new XElement(SbNs + "Status", "Active"),
            new XElement(SbNs + "SupportOrdering", "true"),
            CreateCountDetails(0, 0),
            new XElement(SbNs + "SubscriptionCount", "0"),
            new XElement(SbNs + "EnablePartitioning", "false")
        );

        return CreateEntryElement(name, name, description);
    }

    private static XElement CreateSubscriptionEntry(string topicPath, string subName, long active, long dlq, bool requiresSession)
    {
        var description = new XElement(SbNs + "SubscriptionDescription",
            new XAttribute(XNamespace.Xmlns + "i", XsiNs),
            new XElement(SbNs + "LockDuration", DefaultLockDuration),
            new XElement(SbNs + "RequiresSession", requiresSession.ToString().ToLower()),
            new XElement(SbNs + "DefaultMessageTimeToLive", DefaultTtl),
            new XElement(SbNs + "DeadLetteringOnMessageExpiration", "false"),
            new XElement(SbNs + "DeadLetteringOnFilterEvaluationExceptions", "false"),
            new XElement(SbNs + "MessageCount", (active + dlq).ToString()),
            new XElement(SbNs + "MaxDeliveryCount", "10"),
            new XElement(SbNs + "EnableBatchedOperations", "false"),
            new XElement(SbNs + "Status", "Active"),
            new XElement(SbNs + "CreatedAt", Now),
            new XElement(SbNs + "UpdatedAt", Now),
            new XElement(SbNs + "AccessedAt", Now),
            CreateCountDetails(active, dlq),
            new XElement(SbNs + "AutoDeleteOnIdle", DefaultTtl),
            new XElement(SbNs + "EntityAvailabilityStatus", "Available")
        );

        var path = $"{topicPath}/Subscriptions/{subName}";
        return CreateEntryElement(subName, path, description);
    }

    private static XElement CreateRuleEntry(string topicPath, string subName, string ruleName)
    {
        var description = new XElement(SbNs + "RuleDescription",
            new XElement(SbNs + "Filter",
                new XAttribute(XsiNs + "type", "TrueFilter"),
                new XElement(SbNs + "SqlExpression", "1=1"),
                new XElement(SbNs + "CompatibilityLevel", "20")
            ),
            new XElement(SbNs + "Action",
                new XAttribute(XsiNs + "type", "EmptyRuleAction")
            ),
            new XElement(SbNs + "Name", ruleName)
        );

        var path = $"{topicPath}/Subscriptions/{subName}/Rules/{ruleName}";
        return CreateEntryElement(ruleName, path, description);
    }

    private static XElement CreateEntryElement(string title, string path, XElement description)
    {
        return new XElement(AtomNs + "entry",
            new XElement(AtomNs + "id", $"{BaseUrl}/{path}"),
            new XElement(AtomNs + "title", new XAttribute("type", "text"), title),
            new XElement(AtomNs + "published", Now),
            new XElement(AtomNs + "updated", Now),
            new XElement(AtomNs + "link",
                new XAttribute("rel", "self"),
                new XAttribute("href", $"{BaseUrl}/{path}")
            ),
            new XElement(AtomNs + "content",
                new XAttribute("type", "application/xml"),
                description
            )
        );
    }

    private static XElement CreateCountDetails(long active, long deadletter)
    {
        XNamespace d2p1 = "http://schemas.microsoft.com/netservices/2011/06/servicebus";
        return new XElement(SbNs + "CountDetails",
            new XAttribute(XNamespace.Xmlns + "d2p1", d2p1),
            new XElement(d2p1 + "ActiveMessageCount", active),
            new XElement(d2p1 + "DeadLetterMessageCount", deadletter),
            new XElement(d2p1 + "ScheduledMessageCount", 0),
            new XElement(d2p1 + "TransferMessageCount", 0),
            new XElement(d2p1 + "TransferDeadLetterMessageCount", 0)
        );
    }
}
