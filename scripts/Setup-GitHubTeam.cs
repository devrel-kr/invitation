#:property PublishAot=false

using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

var options = Arguments.Parse(args);
var token = Environment.GetEnvironmentVariable("GH_TOKEN");
if (string.IsNullOrWhiteSpace(token))
{
    token = (await RunAsync("gh", "auth", "token")).Trim();
}

if (string.IsNullOrWhiteSpace(token))
{
    throw new InvalidOperationException(
        "GitHub authentication is required. Set GH_TOKEN or run 'gh auth login'.");
}

var apiUrl = Environment.GetEnvironmentVariable("GH_API_URL")?.TrimEnd('/')
    ?? "https://api.github.com";
using var client = new HttpClient
{
    BaseAddress = new Uri($"{apiUrl}/")
};
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
client.DefaultRequestHeaders.UserAgent.ParseAdd("onboarding-template-setup");
client.DefaultRequestHeaders.Accept.Add(
    new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

Console.WriteLine(
    $"Looking for GitHub team '{options.TeamName}' in organization " +
    $"'{options.GitHubOrganization}'...");
var team = await FindTeamAsync(client, options.GitHubOrganization, options.TeamName);
if (team is null)
{
    Console.WriteLine($"Creating GitHub team '{options.TeamName}'...");
    team = await CreateTeamAsync(client, options);
    Console.WriteLine($"Created GitHub team '{team.Name}' ({team.Id}).");
}
else
{
    Console.WriteLine($"Using existing GitHub team '{team.Name}' ({team.Id}).");
}

Console.WriteLine($"Saving GITHUB_TEAM_ID to repository '{options.GitHubRepo}'...");
await RunAsync(
    "gh",
    "variable", "set", "GITHUB_TEAM_ID",
    "--body", team.Id.ToString(),
    "--repo", options.GitHubRepo);

Console.WriteLine();
Console.WriteLine($"GITHUB_TEAM_ID: {team.Id}");
Console.WriteLine($"Team URL: {team.HtmlUrl}");

static async Task<Team?> FindTeamAsync(
    HttpClient client,
    string organization,
    string teamName)
{
    for (var page = 1; ; page++)
    {
        using var response = await client.GetAsync(
            $"orgs/{Uri.EscapeDataString(organization)}/teams?per_page=100&page={page}");
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"GitHub team lookup failed ({(int)response.StatusCode}): {content}");
        }

        using var document = JsonDocument.Parse(content);
        var teams = document.RootElement.EnumerateArray().ToArray();
        var matches = teams
            .Where(element =>
                string.Equals(
                    RequiredString(element, "name"),
                    teamName,
                    StringComparison.OrdinalIgnoreCase))
            .Select(ParseTeam)
            .ToArray();
        if (matches.Length > 1)
        {
            throw new InvalidOperationException(
                $"More than one GitHub team is named '{teamName}'.");
        }

        if (matches.Length == 1)
        {
            return matches[0];
        }

        if (teams.Length < 100)
        {
            return null;
        }
    }
}

static async Task<Team> CreateTeamAsync(HttpClient client, Arguments options)
{
    using var body = new StringContent(
        JsonSerializer.Serialize(new
        {
            name = options.TeamName,
            description = options.Description,
            privacy = options.Privacy,
            notification_setting = "notifications_enabled"
        }),
        Encoding.UTF8,
        "application/json");
    using var response = await client.PostAsync(
        $"orgs/{Uri.EscapeDataString(options.GitHubOrganization)}/teams",
        body);
    var content = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException(
            $"GitHub team creation failed ({(int)response.StatusCode}): {content}");
    }

    using var document = JsonDocument.Parse(content);
    return ParseTeam(document.RootElement);
}

static Team ParseTeam(JsonElement element)
{
    if (!element.TryGetProperty("id", out var idProperty) ||
        !idProperty.TryGetInt64(out var id))
    {
        throw new InvalidDataException("GitHub team response did not contain numeric 'id'.");
    }

    return new Team(
        id,
        RequiredString(element, "name"),
        RequiredString(element, "html_url"));
}

static string RequiredString(JsonElement element, string propertyName)
{
    if (!element.TryGetProperty(propertyName, out var property) ||
        string.IsNullOrWhiteSpace(property.GetString()))
    {
        throw new InvalidDataException(
            $"GitHub team response did not contain '{propertyName}'.");
    }

    return property.GetString()!;
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

sealed record Team(long Id, string Name, string HtmlUrl);

sealed record Arguments(
    string GitHubOrganization,
    string TeamName,
    string Description,
    string Privacy,
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

        var privacy = values.TryGetValue("--privacy", out var privacyValue)
            ? privacyValue.ToLowerInvariant()
            : "closed";
        if (privacy is not ("closed" or "secret"))
        {
            throw new ArgumentException("--privacy must be 'closed' or 'secret'.");
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

        return new Arguments(
            organization,
            Required("--team-name"),
            values.TryGetValue("--description", out var description)
                ? description
                : "Users onboarded by this workflow.",
            privacy,
            repository);

        string Required(string name) =>
            values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new ArgumentException($"{name} is required.");
    }
}
