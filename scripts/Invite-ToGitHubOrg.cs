#:property PublishAot=false

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

var options = Arguments.Parse(args);
var handle = options.GitHubHandle.Trim();

if (!Regex.IsMatch(
        handle,
        "^[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,37}[a-zA-Z0-9])?$",
        RegexOptions.CultureInvariant))
{
    throw new ArgumentException($"GitHub handle '{options.GitHubHandle}' is invalid.");
}

var token = Environment.GetEnvironmentVariable("GH_TOKEN");
if (string.IsNullOrWhiteSpace(token))
{
    throw new InvalidOperationException("GH_TOKEN is required.");
}

var apiUrl = Environment.GetEnvironmentVariable("GH_API_URL")?.TrimEnd('/')
    ?? "https://api.github.com";
using var client = new HttpClient
{
    BaseAddress = new Uri($"{apiUrl}/")
};
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
client.DefaultRequestHeaders.UserAgent.ParseAdd("devrel-kr-invitation");
client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

Console.WriteLine($"Resolving GitHub user '{handle}'...");
using var userResponse = await client.GetAsync($"users/{Uri.EscapeDataString(handle)}");
var userContent = await userResponse.Content.ReadAsStringAsync();
if (!userResponse.IsSuccessStatusCode)
{
    throw new InvalidOperationException(
        $"GitHub user lookup failed ({(int)userResponse.StatusCode}): {userContent}");
}

using var userDocument = JsonDocument.Parse(userContent);
if (!userDocument.RootElement.TryGetProperty("id", out var idElement) ||
    !idElement.TryGetInt64(out var userId))
{
    throw new InvalidDataException($"GitHub user '{handle}' did not return a numeric ID.");
}

Console.WriteLine(
    $"Inviting GitHub user '{handle}' ({userId}) to organization '{options.Organization}'...");
using var invitationBody = new StringContent(
    JsonSerializer.Serialize(new { invitee_id = userId, role = "direct_member" }),
    Encoding.UTF8,
    "application/json");
using var invitationResponse = await client.PostAsync(
    $"orgs/{Uri.EscapeDataString(options.Organization)}/invitations",
    invitationBody);
var invitationContent = await invitationResponse.Content.ReadAsStringAsync();
if (!invitationResponse.IsSuccessStatusCode)
{
    throw new InvalidOperationException(
        $"GitHub organization invitation failed ({(int)invitationResponse.StatusCode}): " +
        invitationContent);
}

Console.WriteLine($"User '{handle}' invited to organization '{options.Organization}' successfully.");

sealed record Arguments(string Organization, string GitHubHandle)
{
    public static Arguments Parse(string[] args)
    {
        var values = ParseValues(args);
        return new Arguments(Required("--organization"), Required("--github-handle"));

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
