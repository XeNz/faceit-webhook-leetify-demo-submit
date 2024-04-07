using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddLogging();
builder.Services.AddHttpLogging(options => { options.LoggingFields = HttpLoggingFields.All; });
builder.Services.AddOptions<ApplicationSettings>().Bind(builder.Configuration.GetSection(nameof(ApplicationSettings)));
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default));

builder.Services.AddHttpClient<FaceitService>();
builder.Services.AddHttpClient<LeetifyService>();
builder.Services.AddHealthChecks();

var app = builder.Build();


var api = app.MapGroup("/leetify")
    .MapPost("/webhook", async (
        [FromBody] FaceitWebhookRequest request,
        [FromServices] LeetifyService leetifyService,
        [FromServices] FaceitService faceitService
    ) =>
    {
        var demoInformation = await faceitService.DownloadDemoInformation(request.Payload.DemoUrl);
        var loginResponse = await leetifyService.LogIn();
        await leetifyService.SubmitDemoUrl(loginResponse.Token, demoInformation.Payload.DownloadUrl);

        return TypedResults.Accepted((string?)null);
    });

app.MapHealthChecks("/health");
app.Run();


[JsonSerializable(typeof(FaceitWebhookRequest))]
[JsonSerializable(typeof(FaceitWebhookPayload))]
[JsonSerializable(typeof(FaceitDemoRequest))]
[JsonSerializable(typeof(FaceitDemoResponse))]
[JsonSerializable(typeof(FaceitDemoPayload))]
[JsonSerializable(typeof(LeetifyLoginRequest))]
[JsonSerializable(typeof(LeetifyLoginResponse))]
[JsonSerializable(typeof(LeetifyUploadRequest))]
internal partial class AppJsonSerializerContext : JsonSerializerContext;

public record ApplicationSettings
{
    public ApplicationSettings()
    {
    }

    public ApplicationSettings(string faceitUrl, string leetifyUrl, string leetifyUsername, string leetifyPassword)
    {
        FaceitUrl = faceitUrl;
        LeetifyUrl = leetifyUrl;
        LeetifyUsername = leetifyUsername;
        LeetifyPassword = leetifyPassword;
    }

    public string FaceitUrl { get; set; } = default!;
    public string LeetifyUrl { get; set; } = default!;
    public string LeetifyUsername { get; set; } = default!;
    public string LeetifyPassword { get; set; } = default!;

    public void Deconstruct(
        out string faceitUrl,
        out string leetifyUrl,
        out string leetifyUsername,
        out string leetifyPassword)
    {
        faceitUrl = FaceitUrl;
        leetifyUrl = LeetifyUrl;
        leetifyUsername = LeetifyUsername;
        leetifyPassword = LeetifyPassword;
    }
}

public class FaceitService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FaceitService> _logger;

    public FaceitService(HttpClient httpClient, IOptions<ApplicationSettings> applicationSettings,
        ILogger<FaceitService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(applicationSettings.Value.FaceitUrl);
    }

    public async Task<FaceitDemoResponse> DownloadDemoInformation(string resourceUri)
    {
        _logger.LogInformation("Attempting to get demo URL with resourceUri {ResourceUri}", resourceUri);
        var response = await _httpClient.PostAsJsonAsync(
            "/download/v2/demos/download-url",
            new FaceitDemoRequest(resourceUri),
            AppJsonSerializerContext.Default.FaceitDemoRequest);
        response.EnsureSuccessStatusCode();
        var responseBody =
            (await response.Content.ReadFromJsonAsync(AppJsonSerializerContext.Default.FaceitDemoResponse))!;
        _logger.LogInformation("Got download URL: {DownloadUrl}", responseBody.Payload.DownloadUrl);
        return responseBody;
    }
}

public class LeetifyService
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<ApplicationSettings> _applicationSettings;
    private readonly ILogger<LeetifyService> _logger;

    public LeetifyService(
        HttpClient httpClient,
        IOptions<ApplicationSettings> applicationSettings,
        ILogger<LeetifyService> logger
    )
    {
        _httpClient = httpClient;
        _applicationSettings = applicationSettings;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(applicationSettings.Value.LeetifyUrl);
    }

    public async Task<LeetifyLoginResponse> LogIn()
    {
        var (_, _, username, password) = _applicationSettings.Value;
        _logger.LogInformation("Logging in for user {Username}", username);
        var body = new LeetifyLoginRequest(username, password);
        var response =
            await _httpClient.PostAsJsonAsync("/login", body, AppJsonSerializerContext.Default.LeetifyLoginRequest);
        response.EnsureSuccessStatusCode();
        var responseBody = (await response.Content.ReadFromJsonAsync(AppJsonSerializerContext.Default.LeetifyLoginResponse))!;
        _logger.LogInformation("Got Leetify token {Token}", responseBody.Token);
        return responseBody;
    }

    public async Task SubmitDemoUrl(string token, string downloadUrl)
    {
        var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "/faceit-demos/submit-demo-download-url");
        httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var json = JsonSerializer.Serialize(
            new LeetifyUploadRequest(downloadUrl),
            AppJsonSerializerContext.Default.LeetifyUploadRequest
        );

        httpRequestMessage.Content = new StringContent(json, Encoding.UTF8);
        httpRequestMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        _logger.LogInformation("Sending {DownloadUrl} to leetify", downloadUrl);
        var response = await _httpClient.SendAsync(httpRequestMessage);
        response.EnsureSuccessStatusCode();
    }
}

public record FaceitWebhookRequest(FaceitWebhookPayload Payload);

public record FaceitWebhookPayload(string DemoUrl)
{
    [JsonPropertyName("demo_url")] public string DemoUrl { get; set; } = DemoUrl;
}

public record LeetifyLoginRequest(string Email, string Password);

public record LeetifyLoginResponse(string Token);

public record LeetifyUploadRequest(string Url);

public record FaceitDemoRequest(string ResourceUrl)
{
    [JsonPropertyName("resource_url")] public string ResourceUrl { get; set; } = ResourceUrl;
}

public record FaceitDemoResponse(FaceitDemoPayload Payload);

public record FaceitDemoPayload(string DownloadUrl)
{
    [JsonPropertyName("download_url")] public string DownloadUrl { get; set; } = DownloadUrl;
}