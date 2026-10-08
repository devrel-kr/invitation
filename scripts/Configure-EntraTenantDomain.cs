#:property PublishAot=false

using System.Runtime.CompilerServices;
using System.Text;

var options = Arguments.Parse(args);
if (Uri.CheckHostName(options.TenantDomain) != UriHostNameType.Dns)
{
    throw new ArgumentException("--tenant-domain must be a DNS domain name.");
}

var repositoryRoot = FindRepositoryRoot();
const string placeholder = "{{ENTRA_TENANT_DOMAIN_NAME}}";
var targets = new[]
{
    new TenantDomainFile(
        Path.Combine(repositoryRoot, ".github", "ISSUE_TEMPLATE", "onboarding-request-azure-en.yml"),
        $"- \"{options.TenantDomain}\""),
    new TenantDomainFile(
        Path.Combine(repositoryRoot, ".github", "ISSUE_TEMPLATE", "onboarding-request-azure-ko.yml"),
        $"- \"{options.TenantDomain}\""),
    new TenantDomainFile(
        Path.Combine(repositoryRoot, ".github", "workflows", "onboard-user-to-azure.yml"),
        $"EXPECTED_ORGANIZATION: \"{options.TenantDomain}\"")
};
var updates = new List<(string Path, string Content)>();

foreach (var target in targets)
{
    if (!File.Exists(target.Path))
    {
        throw new FileNotFoundException(
            $"Required onboarding configuration file was not found: {target.Path}",
            target.Path);
    }

    var content = File.ReadAllText(target.Path);
    if (content.Contains(placeholder, StringComparison.Ordinal))
    {
        updates.Add((
            target.Path,
            content.Replace(placeholder, options.TenantDomain, StringComparison.Ordinal)));
    }
    else if (content.Contains(target.ConfiguredValue, StringComparison.Ordinal))
    {
        Console.WriteLine($"Tenant domain is already configured in '{target.Path}'.");
    }
    else
    {
        throw new InvalidOperationException(
            $"Neither '{placeholder}' nor the requested tenant domain was found in " +
            $"'{target.Path}'. Check the file manually before continuing.");
    }
}

foreach (var update in updates)
{
    File.WriteAllText(update.Path, update.Content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    Console.WriteLine($"Configured tenant domain in '{update.Path}'.");
}

static string FindRepositoryRoot([CallerFilePath] string sourceFilePath = "")
{
    if (string.IsNullOrWhiteSpace(sourceFilePath))
    {
        throw new InvalidOperationException("The helper source-file path was not available.");
    }

    var directory = new FileInfo(Path.GetFullPath(sourceFilePath)).Directory;
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new DirectoryNotFoundException(
        $"Could not find the repository root containing 'global.json' from '{sourceFilePath}'.");
}

sealed record TenantDomainFile(string Path, string ConfiguredValue);

sealed record Arguments(string TenantDomain)
{
    public static Arguments Parse(string[] args)
    {
        if (args.Length != 2 ||
            !string.Equals(args[0], "--tenant-domain", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(args[1]))
        {
            throw new ArgumentException(
                "Usage: Configure-EntraTenantDomain.cs --tenant-domain <verified-domain>");
        }

        return new Arguments(args[1].Trim());
    }
}
