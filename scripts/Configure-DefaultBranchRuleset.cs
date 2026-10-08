#:property PublishAot=false

using System.Diagnostics;

var options = Arguments.Parse(args);
var repositoryPath = $"repos/{options.Repository}/rulesets";
var listResult = await RunGhAsync(
    new[] { "api", "--paginate", "--jq", ".[].name", $"{repositoryPath}?per_page=100" });
EnsureGhSucceeded(listResult, "list repository rulesets");

if (listResult.StandardOutput
    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
    .Any(name => string.Equals(name.Trim(), "default", StringComparison.Ordinal)))
{
    Console.WriteLine(
        $"A ruleset named 'default' already exists for '{options.Repository}'. " +
        "No changes were made; verify that its settings match the requested configuration.");
    return;
}

const string rulesetJson = """
{
  "name": "default",
  "target": "branch",
  "enforcement": "active",
  "bypass_actors": [
    {"actor_id": null, "actor_type": "OrganizationAdmin", "bypass_mode": "always"},
    {"actor_id": 5, "actor_type": "RepositoryRole", "bypass_mode": "always"}
  ],
  "conditions": {
    "ref_name": {
      "include": ["~DEFAULT_BRANCH"],
      "exclude": []
    }
  },
  "rules": [
    { "type": "deletion" },
    { "type": "non_fast_forward" }
  ]
}
""";

var createResult = await RunGhAsync(
    new[]
    {
        "api",
        "--method",
        "POST",
        repositoryPath,
        "--input",
        "-"
    },
    rulesetJson);
EnsureGhSucceeded(createResult, $"create the default ruleset for '{options.Repository}'");

if (!string.IsNullOrWhiteSpace(createResult.StandardOutput))
{
    Console.WriteLine(createResult.StandardOutput.Trim());
}

Console.WriteLine($"Created the default branch ruleset for '{options.Repository}'.");

static async Task<GhResult> RunGhAsync(string[] arguments, string? standardInput = null)
{
    var startInfo = new ProcessStartInfo("gh")
    {
        CreateNoWindow = true,
        RedirectStandardError = true,
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        UseShellExecute = false
    };

    foreach (var argument in arguments)
    {
        startInfo.ArgumentList.Add(argument);
    }

    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException("Could not start the GitHub CLI.");
    var standardOutputTask = process.StandardOutput.ReadToEndAsync();
    var standardErrorTask = process.StandardError.ReadToEndAsync();

    if (standardInput is not null)
    {
        await process.StandardInput.WriteAsync(standardInput);
    }

    process.StandardInput.Close();
    await process.WaitForExitAsync();

    return new GhResult(
        process.ExitCode,
        await standardOutputTask,
        await standardErrorTask);
}

static void EnsureGhSucceeded(GhResult result, string operation)
{
    if (result.ExitCode != 0)
    {
        var details = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;
        throw new InvalidOperationException(
            $"Failed to {operation} with gh (exit code {result.ExitCode}): {details.Trim()}");
    }
}

sealed record GhResult(int ExitCode, string StandardOutput, string StandardError);

sealed record Arguments(string Repository)
{
    public static Arguments Parse(string[] args)
    {
        if (args.Length != 2 ||
            !string.Equals(args[0], "--repo", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Usage: Configure-DefaultBranchRuleset.cs --repo <owner/repository>");
        }

        var repository = args[1].Trim();
        var segments = repository.Split('/');
        if (segments.Length != 2 || segments.Any(segment =>
                segment.Length == 0 ||
                segment is "." or ".." ||
                segment.Any(character =>
                    !char.IsAsciiLetterOrDigit(character) &&
                    character is not '-' and not '_' and not '.')))
        {
            throw new ArgumentException("--repo must be a GitHub owner/repository name.");
        }

        return new Arguments(repository);
    }
}
