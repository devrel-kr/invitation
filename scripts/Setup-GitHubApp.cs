#:property PublishAot=false

using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var options = Arguments.Parse(args);
var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
var listenerUrl = $"http://127.0.0.1:{options.CallbackPort}/";
var codespacesBaseUrl = GetCodespacesBaseUrl(options.CallbackPort);
var registrationBaseUrl = codespacesBaseUrl ?? listenerUrl;
var callbackUrl = new Uri(new Uri(registrationBaseUrl), "callback/").ToString();
var canOpenBrowser = options.OpenBrowser && codespacesBaseUrl is null;
var githubWebUrl = Environment.GetEnvironmentVariable("GH_WEB_URL")?.TrimEnd('/')
    ?? "https://github.com";
var githubApiUrl = Environment.GetEnvironmentVariable("GH_API_URL")?.TrimEnd('/')
    ?? "https://api.github.com";

var manifest = JsonSerializer.Serialize(new
{
    name = options.AppName,
    url = $"{githubWebUrl}/{options.GitHubRepo}",
    redirect_url = callbackUrl,
    description = "Automates Azure and GitHub organization onboarding requests.",
    @public = false,
    default_events = Array.Empty<string>(),
    default_permissions = new
    {
        contents = "read",
        issues = "write",
        members = "write"
    },
    hook_attributes = new
    {
        url = callbackUrl,
        active = false
    }
});

var registrationUrl =
    $"{githubWebUrl}/organizations/{Uri.EscapeDataString(options.GitHubOrganization)}/settings/apps/new";

using var listener = new HttpListener();
listener.Prefixes.Add(listenerUrl);
listener.Start();

Console.WriteLine($"Preparing GitHub App registration for organization '{options.GitHubOrganization}'...");
if (canOpenBrowser)
{
    OpenBrowser(registrationBaseUrl);
}
else
{
    Console.WriteLine($"Open this URL in your browser: {registrationBaseUrl}");
}

Console.WriteLine($"The local callback listener is running at {listenerUrl}");
Console.WriteLine("Complete the GitHub approval within 10 minutes.");

using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
var code = await ReceiveManifestCodeAsync(
    listener,
    registrationUrl,
    manifest,
    state,
    timeout.Token);

using var client = new HttpClient
{
    BaseAddress = new Uri($"{githubApiUrl}/")
};
client.DefaultRequestHeaders.UserAgent.ParseAdd("onboarding-template-setup");
client.DefaultRequestHeaders.Accept.Add(
    new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

Console.WriteLine("Exchanging the manifest code for GitHub App credentials...");
using var conversionResponse = await client.PostAsync(
    $"app-manifests/{Uri.EscapeDataString(code)}/conversions",
    content: null);
var conversionContent = await conversionResponse.Content.ReadAsStringAsync();
if (!conversionResponse.IsSuccessStatusCode)
{
    throw new InvalidOperationException(
        $"GitHub App manifest conversion failed ({(int)conversionResponse.StatusCode}): " +
        conversionContent);
}

using var conversion = JsonDocument.Parse(conversionContent);
var clientId = RequiredString(conversion.RootElement, "client_id");
var slug = RequiredString(conversion.RootElement, "slug");
var privateKey = RequiredString(conversion.RootElement, "pem");

Console.WriteLine($"Saving GitHub App credentials to repository '{options.GitHubRepo}'...");
await RunAsync(
    null,
    "gh",
    "variable", "set", "APP_CLIENT_ID",
    "--body", clientId,
    "--repo", options.GitHubRepo);
await RunAsync(
    privateKey,
    "gh",
    "secret", "set", "APP_PRIVATE_KEY",
    "--repo", options.GitHubRepo);

var installationUrl = $"{githubWebUrl}/apps/{Uri.EscapeDataString(slug)}/installations/new";
Console.WriteLine();
Console.WriteLine($"GitHub App '{slug}' created successfully.");
Console.WriteLine($"APP_CLIENT_ID saved to {options.GitHubRepo}.");
Console.WriteLine($"APP_PRIVATE_KEY saved to {options.GitHubRepo}.");
Console.WriteLine("Install the app on the organization and grant it access to the repository:");
Console.WriteLine(installationUrl);
if (canOpenBrowser)
{
    OpenBrowser(installationUrl);
}

static string? GetCodespacesBaseUrl(int port)
{
    if (!string.Equals(
            Environment.GetEnvironmentVariable("CODESPACES"),
            "true",
            StringComparison.OrdinalIgnoreCase))
    {
        return null;
    }

    var codespaceName = Environment.GetEnvironmentVariable("CODESPACE_NAME");
    var forwardingDomain = Environment.GetEnvironmentVariable("GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN");
    if (string.IsNullOrWhiteSpace(codespaceName) ||
        string.IsNullOrWhiteSpace(forwardingDomain))
    {
        throw new InvalidOperationException(
            "CODESPACE_NAME and GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN are required in GitHub Codespaces.");
    }

    var expectedHost = $"{codespaceName}-{port}.{forwardingDomain}";
    var forwardedUrl = $"https://{expectedHost}/";
    if (!Uri.TryCreate(forwardedUrl, UriKind.Absolute, out var uri) ||
        !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(uri.Host, expectedHost, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "GitHub Codespaces did not provide a valid HTTPS port-forwarding URL.");
    }

    return uri.ToString();
}

static async Task<string> ReceiveManifestCodeAsync(
    HttpListener listener,
    string registrationUrl,
    string manifest,
    string state,
    CancellationToken cancellationToken)
{
    while (true)
    {
        var context = await listener.GetContextAsync().WaitAsync(cancellationToken);
        var path = context.Request.Url?.AbsolutePath ?? "/";

        if (path == "/")
        {
            var html = $"""
                <!doctype html>
                <html lang="en">
                <head><meta charset="utf-8"><title>Create GitHub App</title></head>
                <body>
                  <p>Redirecting to GitHub App registration...</p>
                  <form id="manifest" method="post" action="{WebUtility.HtmlEncode(registrationUrl)}">
                    <input type="hidden" name="manifest" value="{WebUtility.HtmlEncode(manifest)}">
                    <input type="hidden" name="state" value="{WebUtility.HtmlEncode(state)}">
                  </form>
                  <script>document.getElementById('manifest').submit();</script>
                </body>
                </html>
                """;
            await RespondAsync(context.Response, HttpStatusCode.OK, html);
            continue;
        }

        if (path == "/callback/")
        {
            var returnedState = context.Request.QueryString["state"];
            var code = context.Request.QueryString["code"];
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(returnedState ?? ""),
                    Encoding.UTF8.GetBytes(state)))
            {
                await RespondAsync(
                    context.Response,
                    HttpStatusCode.BadRequest,
                    "The GitHub App registration state did not match.");
                throw new InvalidOperationException(
                    "GitHub App registration returned an invalid state value.");
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                await RespondAsync(
                    context.Response,
                    HttpStatusCode.BadRequest,
                    "GitHub did not return a manifest code.");
                throw new InvalidOperationException(
                    "GitHub App registration did not return a manifest code.");
            }

            await RespondAsync(
                context.Response,
                HttpStatusCode.OK,
                "GitHub App approved. You can close this page and return to the terminal.");
            return code;
        }

        await RespondAsync(context.Response, HttpStatusCode.NotFound, "Not found.");
    }
}

static async Task RespondAsync(
    HttpListenerResponse response,
    HttpStatusCode statusCode,
    string body)
{
    var bytes = Encoding.UTF8.GetBytes(body);
    response.StatusCode = (int)statusCode;
    response.ContentType = "text/html; charset=utf-8";
    response.ContentLength64 = bytes.Length;
    await response.OutputStream.WriteAsync(bytes);
    response.Close();
}

static void OpenBrowser(string url)
{
    try
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Could not open the browser automatically: {exception.Message}");
    }
}

static string RequiredString(JsonElement element, string propertyName)
{
    if (!element.TryGetProperty(propertyName, out var property) ||
        string.IsNullOrWhiteSpace(property.GetString()))
    {
        throw new InvalidDataException($"Response did not contain '{propertyName}'.");
    }

    return property.GetString()!;
}

static async Task<string> RunAsync(
    string? standardInput,
    string fileName,
    params string[] arguments)
{
    var startInfo = new ProcessStartInfo(fileName)
    {
        RedirectStandardError = true,
        RedirectStandardInput = standardInput is not null,
        RedirectStandardOutput = true,
        UseShellExecute = false
    };

    foreach (var argument in arguments)
    {
        startInfo.ArgumentList.Add(argument);
    }

    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException($"Failed to start '{fileName}'.");
    if (standardInput is not null)
    {
        await process.StandardInput.WriteAsync(standardInput);
        process.StandardInput.Close();
    }

    var outputTask = process.StandardOutput.ReadToEndAsync();
    var errorTask = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    var output = await outputTask;
    var error = await errorTask;

    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException(
            $"'{fileName} {string.Join(' ', arguments)}' failed with exit code " +
            $"{process.ExitCode}: {error}");
    }

    if (!string.IsNullOrWhiteSpace(error))
    {
        Console.Error.Write(error);
    }

    return output;
}

sealed record Arguments(
    string AppName,
    string GitHubOrganization,
    string GitHubRepo,
    int CallbackPort,
    bool OpenBrowser)
{
    public static Arguments Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var noOpen = false;
        for (var index = 0; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--no-open", StringComparison.OrdinalIgnoreCase))
            {
                noOpen = true;
                continue;
            }

            if (!args[index].StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length)
            {
                throw new ArgumentException($"Invalid argument: {args[index]}");
            }

            values[args[index]] = args[++index];
        }

        var organization = Required("--github-org");
        var repository = Required("--github-repo");
        var repositoryParts = repository.Split('/', 2);
        if (repositoryParts.Length != 2 ||
            string.IsNullOrWhiteSpace(repositoryParts[1]) ||
            !string.Equals(
                repositoryParts[0],
                organization,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "--github-repo must use OWNER/REPOSITORY format and belong to --github-org.");
        }

        var port = 53682;
        if (values.TryGetValue("--callback-port", out var portValue) &&
            !int.TryParse(portValue, out port))
        {
            throw new ArgumentException("--callback-port must be an integer.");
        }

        if (port is < 1024 or > 65535)
        {
            throw new ArgumentOutOfRangeException(
                nameof(args),
                "--callback-port must be between 1024 and 65535.");
        }

        return new Arguments(
            Required("--app-name"),
            organization,
            repository,
            port,
            !noOpen);

        string Required(string name) =>
            values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new ArgumentException($"{name} is required.");
    }
}
