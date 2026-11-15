using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using AutoService_web.Domain;
using AutoService_web.Services;
using System.Diagnostics;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

var serviceName = "autoservice-api";
var otlpEndpoint = "http://localhost:4318";

// Джерело для власних бізнес-атрибутів у трейсах
var appActivitySource = new ActivitySource(serviceName);

// 1. Налаштування Traces та Metrics
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing => tracing
        .AddSource(serviceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(opt =>
        {
            opt.Endpoint = new Uri($"{otlpEndpoint}/v1/traces");
            opt.Protocol = OtlpExportProtocol.HttpProtobuf;
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(opt =>
        {
            opt.Endpoint = new Uri($"{otlpEndpoint}/v1/metrics");
            opt.Protocol = OtlpExportProtocol.HttpProtobuf;
        }))
    .WithLogging(logging => logging
        .AddOtlpExporter(opt =>
        {
            opt.Endpoint = new Uri($"{otlpEndpoint}/v1/logs");
            opt.Protocol = OtlpExportProtocol.HttpProtobuf;
        }));

// Щоб у Loki передавався сам текст повідомлення, а не лише шаблон
builder.Logging.AddOpenTelemetry(options =>
{
    options.IncludeFormattedMessage = true;
    options.IncludeScopes = true;
});

builder.Services.AddCors();
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(@"Data Source=autoservice.db"));

builder.Services.AddScoped<CatalogService>();
builder.Services.AddSingleton<IdempotencyService>();

var app = builder.Build();

app.UseCors(x => x
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader()
    .WithExposedHeaders("X-Request-Id", "Retry-After"));

app.UseDefaultFiles();
app.UseStaticFiles();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    if (!db.Items.Any())
    {
        db.Items.AddRange(
            new Item { Name = "Діагностика" },
            new Item { Name = "Ремонт двигуна" }
        );
        db.SaveChanges();
    }
}

// Correlation ID (X-Request-Id)
app.Use(async (context, next) =>
{
    var requestId = context.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString();
    context.Items["RequestId"] = requestId;
    context.Response.Headers["X-Request-Id"] = requestId;
    await next();
});

// Simple Rate Limiter
var rateStore = new ConcurrentDictionary<string, (int Count, DateTime Ts)>();
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/health" || context.Request.Path.Value?.StartsWith("/js") == true)
    {
        await next();
        return;
    }

    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "local";
    var now = DateTime.UtcNow;
    var reqId = context.Items["RequestId"]?.ToString();

    var state = rateStore.AddOrUpdate(ip,
        _ => (1, now),
        (_, old) => (now - old.Ts).TotalSeconds < 10 ? (old.Count + 1, old.Ts) : (1, now));

    if (state.Count > 8)
    {
        context.Response.Headers["Retry-After"] = "2";
        context.Response.StatusCode = 429;
        await context.Response.WriteAsJsonAsync(new ErrorResponse("too_many_requests", null, null, reqId));
        return;
    }
    await next();
});

// Fault & Delay Injection
app.Use(async (context, next) =>
{
    if (context.Request.Path.Value?.Equals("/orders", StringComparison.OrdinalIgnoreCase) == true
        && context.Request.Method == "POST")
    {
        var rand = Random.Shared.NextDouble();
        var reqId = context.Items["RequestId"]?.ToString();

        if (rand < 0.15)
            await Task.Delay(Random.Shared.Next(1200, 2000));

        if (rand > 0.80)
        {
            var isUnavailable = Random.Shared.NextDouble() < 0.5;
            context.Response.StatusCode = isUnavailable ? 503 : 500;
            await context.Response.WriteAsJsonAsync(new ErrorResponse(
                Error: isUnavailable ? "unavailable" : "unexpected",
                Code: null,
                Details: null,
                RequestId: reqId
            ));
            return;
        }
    }

    await next();
});

app.MapGet("/", () => Results.File(Path.Combine(builder.Environment.WebRootPath, "index.html"), "text/html"));

// Health Check
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Ендпоінт із кастомними атрибутами для трейсів та логів
app.MapGet("/api/orders/{id:int}", (int id, ILogger<Program> logger) =>
{
    Activity.Current?.SetTag("order.id", id);
    Activity.Current?.SetTag("service.category", "diagnostics");

    logger.LogInformation("Отримано запит на замовлення СТО #{OrderId}", id);
    return Results.Ok(new { OrderId = id, Status = "InProgress", Car = "Opel Zafira B" });
});

// Ендпоінт для симуляції помилок (500) та затримок (Latency) при перевірці алертів
app.MapGet("/api/chaos", async (ILogger<Program> logger, bool fail = false, int delayMs = 0) =>
{
    Activity.Current?.SetTag("chaos.fail", fail);
    Activity.Current?.SetTag("chaos.delay_ms", delayMs);

    if (delayMs > 0)
    {
        logger.LogWarning("Штучна затримка обробки запиту: {DelayMs} мс", delayMs);
        await Task.Delay(delayMs);
    }

    if (fail)
    {
        logger.LogError("Критична помилка сервісу СТО під час обробки запиту!");
        return Results.Problem("Simulated AutoService internal error", statusCode: 500);
    }

    return Results.Ok(new { status = "ok", delayMs });
});

// Ідемпотентний POST /orders
app.MapPost("/orders", (HttpContext ctx, IdempotencyService idemService, OrderCreateDto dto) =>
{
    var reqId = ctx.Items["RequestId"]?.ToString();
    if (!ctx.Request.Headers.TryGetValue("Idempotency-Key", out var key) || string.IsNullOrEmpty(key))
    {
        return Results.Json(new ErrorResponse("idempotency_key_required", null, null, reqId), statusCode: 400);
    }

    var order = idemService.GetOrAdd(key!, () => new Order("ord_" + Guid.NewGuid().ToString()[..8], dto.Title ?? "Untitled"));

    return Results.Json(new { order.Id, order.Title, requestId = reqId }, statusCode: 201);
});

app.MapGet("/services", (CatalogService service) => Results.Ok(service.GetAll()));

app.MapGet("/services/{id}", (int id, CatalogService service) =>
{
    var result = service.GetById(id);
    return result != null ? Results.Ok(result) : Results.NotFound(new ErrorResponse("Not Found", "ITEM_NOT_FOUND"));
});

app.MapPost("/services", (ServiceItemRequest req, CatalogService service) =>
{
    try
    {
        var result = service.Create(req);
        return Results.Created($"/services/{result.Id}", result);
    }
    catch (ArgumentException)
    {
        return Results.BadRequest(new ErrorResponse("Validation Error", "NAME_REQUIRED"));
    }
});

app.MapPut("/services/{id}", (int id, ServiceItemRequest req, CatalogService service) =>
{
    try
    {
        var result = service.Update(id, req);
        return result != null ? Results.Ok(result) : Results.NotFound(new ErrorResponse("Not Found", "ITEM_NOT_FOUND"));
    }
    catch (ArgumentException)
    {
        return Results.BadRequest(new ErrorResponse("Validation Error", "NAME_REQUIRED"));
    }
});

app.MapDelete("/services/{id}", (int id, CatalogService service) =>
{
    var success = service.Delete(id);
    return success ? Results.NoContent() : Results.NotFound(new ErrorResponse("Not Found", "ITEM_NOT_FOUND"));
});

app.Run();

record OrderCreateDto(string? Title);