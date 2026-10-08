#:property PublishAot=false

using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

var options = Arguments.Parse(args);
var escapedName = options.GroupName.Replace("'", "''", StringComparison.Ordinal);

Console.WriteLine($"Looking for Microsoft Entra security group '{options.GroupName}'...");
using var existingGroups = JsonDocument.Parse(await RunAsync(
    "az",
    "ad", "group", "list",
    "--filter", $"displayName eq '{escapedName}'",
    "--output", "json"));

var matches = existingGroups.RootElement
    .EnumerateArray()
    .Where(group =>
        string.Equals(
            OptionalString(group, "displayName"),
            options.GroupName,
            StringComparison.OrdinalIgnoreCase))
    .ToArray();

if (matches.Length > 1)
{
    throw new InvalidOperationException(
        $"More than one Microsoft Entra group is named '{options.GroupName}'. " +
        "Rename the duplicate groups before running this setup.");
}

string groupId;
if (matches.Length == 1)
{
    EnsureSecurityGroup(matches[0], options.GroupName);
    groupId = RequiredString(matches[0], "id");
    Console.WriteLine($"Using existing security group '{options.GroupName}' ({groupId}).");
}
else
{
    var mailNickname = options.MailNickname ?? CreateMailNickname(options.GroupName);
    Console.WriteLine($"Creating Microsoft Entra security group '{options.GroupName}'...");
    using var createdGroup = JsonDocument.Parse(await RunAsync(
        "az",
        "ad", "group", "create",
        "--display-name", options.GroupName,
        "--mail-nickname", mailNickname,
        "--description", options.Description,
        "--output", "json"));
    EnsureSecurityGroup(createdGroup.RootElement, options.GroupName);
    groupId = RequiredString(createdGroup.RootElement, "id");
    Console.WriteLine($"Created security group '{options.GroupName}' ({groupId}).");
}

Console.WriteLine($"Saving AZURE_SECURITY_GROUP_ID to repository '{options.GitHubRepo}'...");
await RunAsync(
    "gh",
    "variable", "set", "AZURE_SECURITY_GROUP_ID",
    "--body", groupId,
    "--repo", options.GitHubRepo);

Console.WriteLine();
Console.WriteLine($"AZURE_SECURITY_GROUP_ID: {groupId}");

static void EnsureSecurityGroup(JsonElement group, string groupName)
{
    if (!group.TryGetProperty("securityEnabled", out var securityEnabled) ||
        securityEnabled.ValueKind != JsonValueKind.True)
    {
        throw new InvalidOperationException(
            $"Microsoft Entra group '{groupName}' exists but is not security-enabled.");
    }
}

static string CreateMailNickname(string groupName)
{
    var nickname = Regex.Replace(
            groupName.ToLowerInvariant(),
            "[^a-z0-9]+",
            "-",
            RegexOptions.CultureInvariant)
        .Trim('-');
    return string.IsNullOrWhiteSpace(nickname)
        ? $"onboarding-{Guid.NewGuid():N}"
        : nickname;
}

static string? OptionalString(JsonElement element, string propertyName) =>
    element.TryGetProperty(propertyName, out var property)
        ? property.GetString()
        : null;

static string RequiredString(JsonElement element, string propertyName)
{
    var value = OptionalString(element, propertyName);
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidDataException($"Response did not contain '{propertyName}'.");
    }

    return value;
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

sealed record Arguments(
    string GroupName,
    string Description,
    string? MailNickname,
    string GitHubRepo)
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

        return new Arguments(
            Required("--group-name"),
            values.TryGetValue("--description", out var description)
                ? description
                : "Users onboarded by this workflow.",
            values.TryGetValue("--mail-nickname", out var mailNickname)
                ? mailNickname
                : null,
            Required("--github-repo"));

        string Required(string name) =>
            values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new ArgumentException($"{name} is required.");
    }
}
