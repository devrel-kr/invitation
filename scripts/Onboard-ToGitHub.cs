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
client.DefaultRequestHeaders.UserAgent.ParseAdd("onboarding-template");
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
    $"Preparing organization onboarding for GitHub user '{handle}' ({userId}) " +
    $"in organization '{options.Organization}' " +
    $"and team '{options.TeamId}'...");
using var requestBody = new StringContent(
    JsonSerializer.Serialize(new
    {
        invitee_id = userId,
        role = "direct_member",
        team_ids = new[] { options.TeamId }
    }),
    Encoding.UTF8,
    "application/json");
using var membershipResponse = await client.PostAsync(
    $"orgs/{Uri.EscapeDataString(options.Organization)}/invitations",
    requestBody);
var responseContent = await membershipResponse.Content.ReadAsStringAsync();
if (!membershipResponse.IsSuccessStatusCode)
{
    throw new InvalidOperationException(
        $"GitHub organization onboarding failed ({(int)membershipResponse.StatusCode}): " +
        responseContent);
}

Console.WriteLine(
    $"Onboarding was initiated for user '{handle}' in organization '{options.Organization}' " +
    $"with team '{options.TeamId}'. The user must complete the membership flow before access is granted.");

sealed record Arguments(string Organization, string GitHubHandle, long TeamId)
{
    public static Arguments Parse(string[] args)
    {
        var values = ParseValues(args);
        var teamIdValue = Required("--team-id");
        if (!long.TryParse(teamIdValue, out var teamId) || teamId <= 0)
        {
            throw new ArgumentException("--team-id must be a positive integer.");
        }

        return new Arguments(
            Required("--organization"),
            Required("--github-handle"),
            teamId);

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
