#:property PublishAot=false

using System.Diagnostics;
using System.Text.Json;

var options = Arguments.Parse(args);
var requestBody = JsonSerializer.Serialize(new
{
    invitedUserEmailAddress = options.Email,
    inviteRedirectUrl = "https://portal.azure.com",
    sendInvitationMessage = true,
    invitedUserDisplayName = options.Name
});

var responseJson = await RunAsync(
    "az",
    "rest",
    "--method", "POST",
    "--url", "https://graph.microsoft.com/v1.0/invitations",
   "--body", requestBody,
    "--headers", "Content-Type=application/json");

using var response = JsonDocument.Parse(responseJson);
if (!response.RootElement.TryGetProperty("invitedUser", out var userElement) ||
   !userElement.TryGetProperty("id", out var idElement) ||
    string.IsNullOrWhiteSpace(idElement.GetString()))
{
   throw new InvalidDataException("Microsoft Graph response did not contain an onboarded user ID.");
}

var userId = idElement.GetString()!;
Console.WriteLine($"Azure user ID: {userId}");

var groupId = (await RunAsync(
    "az",
    "ad", "group", "show",
    "--group", options.SecurityGroup,
    "--query", "id",
    "--output", "tsv")).Trim();
if (string.IsNullOrWhiteSpace(groupId))
{
    throw new InvalidOperationException($"Security group '{options.SecurityGroup}' was not found.");
}

await RunAsync(
    "az",
    "ad", "group", "member", "add",
    "--group", groupId,
    "--member-id", userId);

Console.WriteLine(
    $"User '{options.Email}' onboarded and added to security group '{options.SecurityGroup}' successfully.");

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

sealed record Arguments(string Email, string Name, string SecurityGroup)
{
    public static Arguments Parse(string[] args)
    {
        var values = ParseValues(args);
        return new Arguments(
            Required("--email"),
            Required("--name"),
            Required("--security-group"));

        string Required(string name) =>
            values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new ArgumentException($"{name} is required.");
    }

    private static Dictionary<string, string> ParseValues(string[] args)
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

        return values;
    }
}
