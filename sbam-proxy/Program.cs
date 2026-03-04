using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using SbamProxy;

var builder = WebApplication.CreateBuilder(args);

// Read config path from environment or default
var configPath = Environment.GetEnvironmentVariable("EMULATOR_CONFIG_PATH") ?? "/app/config.json";
var config = new EmulatorConfig(configPath);
var fetcher = new CountsFetcher();
Console.WriteLine("[SBAM-Proxy] CountsFetcher instance created.");

// Generate self-signed certificate for HTTPS
var cert = GetCert();

// Configure Kestrel to listen on HTTPS:443
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(443, listenOptions =>
    {
        listenOptions.UseHttps(cert);
        listenOptions.Protocols = HttpProtocols.Http1;
    });
});

var app = builder.Build();

// Middleware: log all requests and responses with BODY for debugging
app.Use(async (context, next) =>
{
    var path = $"{context.Request.Method} {context.Request.Path}{context.Request.QueryString}";
    Console.WriteLine($"[SBAM-Proxy] ---> {path}");
    
    // Capture response body
    var originalBody = context.Response.Body;
    using var memStream = new MemoryStream();
    context.Response.Body = memStream;
    
    await next();
    
    memStream.Position = 0;
    var responseBody = await new StreamReader(memStream).ReadToEndAsync();
    Console.WriteLine($"[SBAM-Proxy] <--- {path} (Status: {context.Response.StatusCode})");
    if (responseBody.Length > 0 && responseBody.Length < 5000)
    {
        Console.WriteLine($"[SBAM-Proxy] BODY:\n{responseBody}");
    }
    else if (responseBody.Length >= 5000)
    {
        Console.WriteLine($"[SBAM-Proxy] BODY (truncated to 2000 chars):\n{responseBody.Substring(0, 2000)}...");
    }
    
    // Copy back to original stream
    memStream.Position = 0;
    await memStream.CopyToAsync(originalBody);
    context.Response.Body = originalBody;
});

// ============================================================
// ENDPOINTS - Simulating Azure Service Bus Management REST API
// ============================================================

// --- List Queues ---
app.MapGet("/$Resources/Queues", async (HttpContext ctx) =>
{
    var queues = config.GetQueueNames();
    var xml = await AtomFeedGenerator.GenerateQueuesFeedWithCounts(queues, async (q) => {
        var counts = await fetcher.GetQueueCountsAsync(q);
        var sessions = config.GetQueueSessionSupport(q);
        return (counts.active, counts.dlq, sessions);
    });
    return Results.Content(xml, "application/atom+xml");
});

// --- List Topics ---
app.MapGet("/$Resources/Topics", (HttpContext ctx) =>
{
    var topics = config.GetTopicNames();
    Console.WriteLine($"[SBAM-Proxy] Returning {topics.Count} topic(s)");
    return Results.Content(AtomFeedGenerator.GenerateTopicsFeed(topics), "application/atom+xml");
});

// --- List EventHubs (empty - emulator doesn't support them) ---
app.MapGet("/$Resources/EventHubs", () =>
{
    Console.WriteLine("[SBAM-Proxy] EventHubs requested - returning empty feed");
    return Results.Content(AtomFeedGenerator.GenerateEmptyFeed("EventHubs", "$Resources/EventHubs"), "application/atom+xml");
});

// --- List Relays (empty) ---
app.MapGet("/$Resources/Relays", () =>
{
    Console.WriteLine("[SBAM-Proxy] Relays requested - returning empty feed");
    return Results.Content(AtomFeedGenerator.GenerateEmptyFeed("Relays", "$Resources/Relays"), "application/atom+xml");
});

// --- List NotificationHubs (empty) ---
app.MapGet("/$Resources/NotificationHubs", () =>
{
    Console.WriteLine("[SBAM-Proxy] NotificationHubs requested - returning empty feed");
    return Results.Content(AtomFeedGenerator.GenerateEmptyFeed("NotificationHubs", "$Resources/NotificationHubs"), "application/atom+xml");
});

// --- List Rules for a Subscription ---
// Must be before the Subscriptions catch-all
app.MapGet("/{topicPath}/Subscriptions/{subName}/Rules", (string topicPath, string subName) =>
{
    Console.WriteLine($"[SBAM-Proxy] Rules for {topicPath}/{subName}");
    return Results.Content(AtomFeedGenerator.GenerateRulesFeed(topicPath, subName), "application/atom+xml");
});

// --- Get single Subscription ---
app.MapGet("/{topicPath}/Subscriptions/{subName}", async (string topicPath, string subName) =>
{
    Console.WriteLine($"[SBAM-Proxy] ROUTE: Get single sub '{topicPath}/{subName}'");
    if (config.SubscriptionExists(topicPath, subName))
    {
        var counts = await fetcher.GetSubscriptionCountsAsync(topicPath, subName);
        var sessions = config.GetSubscriptionSessionSupport(topicPath, subName);
        Console.WriteLine($"[SBAM-Proxy] Fetched counts: {counts.active}/{counts.dlq}, Sessions: {sessions}");
        return Results.Content(AtomFeedGenerator.GenerateSubscriptionEntry(topicPath, subName, counts.active, counts.dlq, sessions), "application/atom+xml");
    }
    Console.WriteLine($"[SBAM-Proxy] Subscription {topicPath}/{subName} NOT found");
    return Results.NotFound();
});

// --- List Subscriptions for a Topic ---
app.MapGet("/{topicPath}/Subscriptions", async (string topicPath) =>
{
    Console.WriteLine($"[SBAM-Proxy] ROUTE: List subs for topic '{topicPath}'");
    var subNames = config.GetSubscriptionNames(topicPath);
    Console.WriteLine($"[SBAM-Proxy] Returning {subNames.Count} subscription(s) for topic {topicPath} (with real counts)");
    var xml = await AtomFeedGenerator.GenerateSubscriptionsFeedWithCounts(topicPath, subNames, async (sub) => {
        var counts = await fetcher.GetSubscriptionCountsAsync(topicPath, sub);
        var sessions = config.GetSubscriptionSessionSupport(topicPath, sub);
        return (counts.active, counts.dlq, sessions);
    });
    return Results.Content(xml, "application/atom+xml");
});

// --- Get single entity (queue or topic) ---
// This catch-all must be last
app.MapGet("/{entityPath}", async (string entityPath, HttpContext ctx) =>
{
    // Check for $namespaceinfo
    if (entityPath.Equals("$namespaceinfo", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("[SBAM-Proxy] Routing $namespaceinfo Request");
        return Results.Content(AtomFeedGenerator.GenerateNamespaceInfoEntry(), "application/atom+xml;type=entry;charset=utf-8");
    }

    // Check if it's a queue
    if (config.QueueExists(entityPath))
    {
        var counts = await fetcher.GetQueueCountsAsync(entityPath);
        var sessions = config.GetQueueSessionSupport(entityPath);
        return Results.Content(AtomFeedGenerator.GenerateQueueEntry(entityPath, counts.active, counts.dlq, sessions), "application/atom+xml");
    }
    // Check if it's a topic
    if (config.TopicExists(entityPath))
    {
        Console.WriteLine($"[SBAM-Proxy] Topic '{entityPath}' found");
        return Results.Content(AtomFeedGenerator.GenerateTopicEntry(entityPath), "application/atom+xml");
    }
    Console.WriteLine($"[SBAM-Proxy] Entity '{entityPath}' NOT found");
    return Results.NotFound();
});

// --- Fallback ---
app.MapFallback((HttpContext ctx) =>
{
    Console.WriteLine($"[SBAM-Proxy] !!! FALLBACK HIT !!! {ctx.Request.Method} {ctx.Request.Path}{ctx.Request.QueryString}");
    return Results.NotFound();
});

Console.WriteLine("==============================================");
Console.WriteLine("  Service Bus Azure Management Proxy");
Console.WriteLine("        (SBAM-Proxy) v1.0.0");
Console.WriteLine("==============================================");
Console.WriteLine($"  Config: {configPath}");
Console.WriteLine($"  Queues: {string.Join(", ", config.GetQueueNames())}");
Console.WriteLine($"  Topics: {string.Join(", ", config.GetTopicNames())}");
Console.WriteLine("  Listening on HTTPS :443");
Console.WriteLine("==============================================");

// --- TCP/SSL Proxy for AMQP (5671 -> servicebus-emulator:5672) ---
_ = Task.Run(async () =>
{
    var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Any, 5671);
    listener.Start();
    Console.WriteLine("  AMQP SSL Proxy: Listening on 5671 -> servicebus-emulator:5672");
    while (true)
    {
        try
        {
            var client = await listener.AcceptTcpClientAsync();
            _ = Task.Run(async () =>
            {
                try
                {
                    using (client)
                    {
                        var stream = client.GetStream();
                        using var sslStream = new System.Net.Security.SslStream(stream, false);
                        await sslStream.AuthenticateAsServerAsync(cert, false, System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13, false);
                        
                        // Use the correct emulator host (defaulting to name in docker-compose)
                        var targetHost = Environment.GetEnvironmentVariable("EMULATOR_HOST") ?? "servicebus-emulator";
                        using var targetClient = new System.Net.Sockets.TcpClient();
                        await targetClient.ConnectAsync(targetHost, 5672);
                        using var targetStream = targetClient.GetStream();

                        var t1 = sslStream.CopyToAsync(targetStream);
                        var t2 = targetStream.CopyToAsync(sslStream);
                        await Task.WhenAny(t1, t2);
                    }
                }
                catch (Exception) { }
            });
        }
        catch (Exception) { }
    }
});

app.Run();

// --- Use Static PFX file ---
static X509Certificate2 GetCert()
{
    var path = "/app/localhost.pfx";
    if (!System.IO.File.Exists(path)) {
        throw new System.Exception("Missing localhost.pfx");
    }
    return new X509Certificate2(path, "pass", X509KeyStorageFlags.Exportable);
}

