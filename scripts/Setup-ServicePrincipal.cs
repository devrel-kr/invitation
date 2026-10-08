#:property PublishAot=false

using System.Diagnostics;
using System.Text.Json;

var options = Arguments.Parse(args);
const string Branch = "main";
const string GraphApiId = "00000003-0000-0000-c000-000000000000";
string[] roleIds =
[
    "09850681-111b-4a89-9bed-3f2cae46d706",
    "dbaae8cf-10b5-4b86-a4a1-f871c94c6695",
    "5b567255-7703-4780-807c-7be8301ae99b"
];

Console.WriteLine("Retrieving current Azure subscription and tenant info...");
var subscriptionId = (await RunAsync("az", "account", "show", "--query", "id", "-o", "tsv")).Trim();
var tenantId = (await RunAsync("az", "account", "show", "--query", "tenantId", "-o", "tsv")).Trim();

Console.WriteLine($"Creating app registration '{options.AppName}'...");
using var app = JsonDocument.Parse(await RunAsync(
    "az", "ad", "app", "create", "--display-name", options.AppName));
var appId = RequiredString(app.RootElement, "appId");
var objectId = RequiredString(app.RootElement, "id");
Console.WriteLine($"Created app registration: {options.AppName} ({appId})");

Console.WriteLine($"Creating service principal for app '{options.AppName}'...");
await RunAsync("az", "ad", "sp", "create", "--id", appId);

var federatedCredential = JsonSerializer.Serialize(new
{
    name = $"azure-onboarding-github-actions-{Branch}",
    issuer = "https://token.actions.githubusercontent.com",
    subject = $"repo:{options.GitHubRepo}:ref:refs/heads/{Branch}",
    audiences = new[] { "api://AzureADTokenExchange" },
    description = $"GitHub Actions OIDC for {options.GitHubRepo} ({Branch})"
});

Console.WriteLine("Creating federated credential for GitHub Actions OIDC...");
await WithTemporaryJsonAsync(federatedCredential, path =>
    RunAsync(
        "az", "ad", "app", "federated-credential", "create",
        "--id", objectId,
        "--parameters", $"@{path}"));

Console.WriteLine(
    "Adding Microsoft Graph API permissions " +
    "(User.Invite.All, GroupMember.ReadWrite.All, Group.Read.All)...");
await RunAsync(
    "az", "ad", "app", "permission", "add",
    "--id", appId,
    "--api", GraphApiId,
    "--api-permissions",
    "09850681-111b-4a89-9bed-3f2cae46d706=Role",
    "dbaae8cf-10b5-4b86-a4a1-f871c94c6695=Role",
    "5b567255-7703-4780-807c-7be8301ae99b=Role");

Console.WriteLine("Granting admin consent for API permissions...");
var servicePrincipalId = (await RunAsync(
    "az", "ad", "sp", "show", "--id", appId, "--query", "id", "-o", "tsv")).Trim();
var graphServicePrincipalId = (await RunAsync(
    "az", "ad", "sp", "show", "--id", GraphApiId, "--query", "id", "-o", "tsv")).Trim();

foreach (var roleId in roleIds)
{
    var assignment = JsonSerializer.Serialize(new
    {
        principalId = servicePrincipalId,
        resourceId = graphServicePrincipalId,
        appRoleId = roleId
    });
    await RunAsync(
        "az", "rest",
        "--method", "POST",
        "--url",
        $"https://graph.microsoft.com/v1.0/servicePrincipals/{servicePrincipalId}/appRoleAssignments",
        "--body", assignment,
        "--headers", "Content-Type=application/json");
}

Console.WriteLine($"Assigning Contributor role on subscription '{subscriptionId}'...");
await RunAsync(
    "az", "role", "assignment", "create",
    "--assignee", appId,
    "--role", "Contributor",
    "--scope", $"/subscriptions/{subscriptionId}");

Console.WriteLine($"Saving Azure variables to GitHub repository '{options.GitHubRepo}'...");
await RunAsync(
    "gh", "variable", "set", "AZURE_CLIENT_ID",
    "--body", appId,
    "--repo", options.GitHubRepo);
await RunAsync(
    "gh", "variable", "set", "AZURE_TENANT_ID",
    "--body", tenantId,
    "--repo", options.GitHubRepo);
await RunAsync(
    "gh", "variable", "set", "AZURE_SUBSCRIPTION_ID",
    "--body", subscriptionId,
    "--repo", options.GitHubRepo);

Console.WriteLine();
Console.WriteLine($"=== GitHub repository variables saved to {options.GitHubRepo} ===");
Console.WriteLine($"AZURE_CLIENT_ID:       {appId}");
Console.WriteLine($"AZURE_TENANT_ID:       {tenantId}");
Console.WriteLine($"AZURE_SUBSCRIPTION_ID: {subscriptionId}");

static string RequiredString(JsonElement element, string propertyName)
{
    if (!element.TryGetProperty(propertyName, out var property) ||
        string.IsNullOrWhiteSpace(property.GetString()))
    {
        throw new InvalidDataException($"Response did not contain '{propertyName}'.");
    }

    return property.GetString()!;
}

static async Task WithTemporaryJsonAsync(string json, Func<string, Task<string>> action)
{
    var path = Path.GetTempFileName();
    try
    {
        await File.WriteAllTextAsync(path, json);
        await action(path);
    }
    finally
    {
        File.Delete(path);
    }
}

static async Task<string> RunAsync(string fileName, params string[] arguments)
{
    var startInfo = new ProcessStartInfo(fileName)
    {
        RedirectStandardError = true,
        RedirectStandardOutput = true,
        UseShellExecute = false
    };

    foreach (var argument in arguments)
    {
        startInfo.ArgumentList.Add(argument);
    }

    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException($"Failed to start '{fileName}'.");
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

sealed record Arguments(string AppName, string GitHubRepo)
{
    public static Arguments Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length)
            {
                throw new ArgumentException($"Invalid argument: {args[index]}");
            }

            values[args[index]] = args[++index];
        }

        return new Arguments(Required("--app-name"), Required("--github-repo"));

        string Required(string name) =>
            values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new ArgumentException($"{name} is required.");
    }
}
