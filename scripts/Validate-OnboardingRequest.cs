#:property PublishAot=false

using System.Globalization;
using System.Net.Mail;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

var options = Arguments.Parse(args);
var issue = IssuePayload.Read(options.InputFile);
var body = options.RequestType switch
{
    RequestType.Azure => ValidateAzure(issue.Body, options.Organization),
    RequestType.GitHub => ValidateGitHub(issue.Body, options.Organization),
    _ => throw new InvalidOperationException($"Unsupported request type: {options.RequestType}")
};

if (issue.CreatedAt > options.DueDate)
{
    body.InvalidReasons.Add("The submission deadline has passed.");
}

if (!string.IsNullOrWhiteSpace(body.GitHubHandle) &&
    !string.Equals(body.GitHubHandle, issue.CreatedBy, StringComparison.OrdinalIgnoreCase))
{
    body.InvalidReasons.Add(options.RequestType == RequestType.Azure
        ? "The GitHub profile URL does not match the issue author."
        : "The GitHub handle does not match the issue author.");
}

var result = new ValidationResult(
    issue.Number,
    ToKoreaTime(issue.CreatedAt),
    ToKoreaTime(options.DueDate),
    issue.CreatedBy,
    body.InvalidReasons.Count == 0,
    body.InvalidReasons,
    new OnboardingBody(
        body.Organisation,
        body.GitHubHandle,
        body.Name,
        body.Email));

var jsonOptions = new JsonSerializerOptions
{
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true
};

File.WriteAllText(options.OutputFile, JsonSerializer.Serialize(result, jsonOptions));

if (options.GitHubOutput is not null)
{
    WriteGitHubOutputs(options.GitHubOutput, result);
}

return;

static ValidatedBody ValidateAzure(string issueBody, string expectedOrganization)
{
    var requestType = GetIssueFormValue(issueBody, "Request Type", "요청 유형");
    var organisation = GetIssueFormValue(issueBody, "Organization", "조직")?.TrimEnd('/');
    var profileUrl = GetIssueFormValue(issueBody, "GitHub Profile Link", "GitHub 프로필 링크")?.TrimEnd('/');
    var name = GetIssueFormValue(issueBody, "Name", "이름");
    var email = GetIssueFormValue(issueBody, "Email", "이메일");
    var invalidReasons = new List<string>();
    var hasExpectedRequestType =
        string.Equals(requestType, "Azure subscription onboarding request", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(requestType, "Azure 구독 온보딩 요청", StringComparison.OrdinalIgnoreCase);

    if (!hasExpectedRequestType)
    {
        invalidReasons.Add("The request type is invalid.");
    }

    if (string.IsNullOrWhiteSpace(organisation) ||
        !string.Equals(organisation, expectedOrganization, StringComparison.OrdinalIgnoreCase))
    {
        invalidReasons.Add("The Azure organization URL is invalid.");
    }

    if (!IsGitHubProfileUrl(profileUrl))
    {
        invalidReasons.Add("The GitHub profile URL is invalid.");
    }

    if (string.IsNullOrWhiteSpace(name))
    {
        invalidReasons.Add("The name is invalid.");
    }

    if (!IsAllowedEmail(email))
    {
        invalidReasons.Add("The email address is invalid.");
    }

    var githubHandle = profileUrl?.Replace("https://github.com/", "", StringComparison.Ordinal);
    return new ValidatedBody(organisation, githubHandle, name, email, invalidReasons);
}

static ValidatedBody ValidateGitHub(string issueBody, string expectedOrganization)
{
    var requestType = GetIssueFormValue(issueBody, "Request Type", "요청 유형");
    var organisation = GetIssueFormValue(issueBody, "Organization", "조직");
    var githubHandle = GetIssueFormValue(issueBody, "GitHub Handle", "GitHub 핸들");
    var invalidReasons = new List<string>();
    var hasExpectedRequestType =
        string.Equals(requestType, "GitHub organization onboarding request", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(requestType, "GitHub 조직 온보딩 요청", StringComparison.OrdinalIgnoreCase);

    if (!hasExpectedRequestType)
    {
        invalidReasons.Add("The request type is invalid.");
    }

    if (!string.Equals(organisation, expectedOrganization, StringComparison.OrdinalIgnoreCase))
    {
        invalidReasons.Add("The GitHub organization is invalid.");
    }

    if (!IsGitHubHandle(githubHandle))
    {
        invalidReasons.Add("The GitHub handle is invalid.");
    }

    return new ValidatedBody(organisation, githubHandle, null, null, invalidReasons);
}

static string? GetIssueFormValue(string body, params string[] labels)
{
    foreach (var label in labels)
    {
        var match = Regex.Match(
            body,
            $@"^###\s+{Regex.Escape(label)}\s*\r?\n(.*?)(?=^###\s+|\z)",
            RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.CultureInvariant);

        if (!match.Success)
        {
            continue;
        }

        var value = match.Groups[1].Value.Trim();
        return string.Equals(value, "_No response_", StringComparison.OrdinalIgnoreCase) ? null : value;
    }

    return null;
}

static bool IsGitHubHandle(string? handle) =>
    handle is not null &&
    Regex.IsMatch(
        handle,
        "^[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,37}[a-zA-Z0-9])?$",
        RegexOptions.CultureInvariant);

static bool IsGitHubProfileUrl(string? url)
{
    if (string.IsNullOrWhiteSpace(url) ||
        !Uri.IsWellFormedUriString(url, UriKind.Absolute) ||
        !url.StartsWith("https://github.com/", StringComparison.Ordinal))
    {
        return false;
    }

    return url.Split('/', StringSplitOptions.RemoveEmptyEntries).Length == 3;
}

static bool IsAllowedEmail(string? email)
{
    if (string.IsNullOrWhiteSpace(email) || !MailAddress.TryCreate(email, out var address))
    {
        return false;
    }

    return ValidationConstants.AllowedEmailDomains.Contains(address.Host);
}

static DateTimeOffset ToKoreaTime(DateTimeOffset value) =>
    TimeZoneInfo.ConvertTime(value, TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul"));

static void WriteGitHubOutputs(string path, ValidationResult result)
{
    var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");
    var submittedAt = TimeZoneInfo.ConvertTime(result.CreatedAt, timeZone)
        .ToString("yyyy-MM-dd HH:mm:ss.fff '+09:00'", CultureInfo.InvariantCulture);
    var dueBy = TimeZoneInfo.ConvertTime(result.DueDate, timeZone)
        .ToString("yyyy-MM-dd HH:mm:ss.fff '+09:00'", CultureInfo.InvariantCulture);
    var invalidReasons = $"<ul><li> {string.Join("</li><li> ", result.InvalidReasons)}</li></ul>";

    using var writer = File.AppendText(path);
    WriteOutput(writer, "issueNumber", result.Number.ToString(CultureInfo.InvariantCulture));
    WriteOutput(writer, "submittedAt", submittedAt);
    WriteOutput(writer, "dueBy", dueBy);
    WriteOutput(writer, "isValid", result.IsValid.ToString().ToLowerInvariant());
    WriteOutput(writer, "invalidReasons", invalidReasons);
    WriteOutput(writer, "org", result.Body.Organisation);
    WriteOutput(writer, "githubHandle", result.Body.GitHubHandle);
    WriteOutput(writer, "name", result.Body.Name);
    WriteOutput(writer, "email", result.Body.Email);
}

static void WriteOutput(TextWriter writer, string name, object? value)
{
    var delimiter = $"ghadelimiter_{Guid.NewGuid():N}";
    writer.WriteLine($"{name}<<{delimiter}");
    writer.WriteLine(value);
    writer.WriteLine(delimiter);
}

enum RequestType
{
    Azure,
    GitHub
}

sealed record Arguments(
    RequestType RequestType,
    string InputFile,
    string OutputFile,
    DateTimeOffset DueDate,
    string Organization,
    string? GitHubOutput)
{
    public static Arguments Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            var key = args[index];
            if (!key.StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length)
            {
                throw new ArgumentException($"Invalid argument: {key}");
            }

            values[key] = args[++index];
        }

        var requestType = Required("--request-type").ToLowerInvariant() switch
        {
            "azure" => RequestType.Azure,
            "github" => RequestType.GitHub,
            var value => throw new ArgumentException($"Unsupported request type: {value}")
        };

        if (!DateTimeOffset.TryParse(
                Required("--due-date"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var dueDate))
        {
            throw new ArgumentException("--due-date must be a valid ISO-8601 DateTimeOffset.");
        }

        return new Arguments(
            requestType,
            Path.GetFullPath(Required("--input")),
            Path.GetFullPath(Required("--output")),
            dueDate,
            Required("--organization"),
            values.TryGetValue("--github-output", out var output) ? Path.GetFullPath(output) : null);

        string Required(string name) =>
            values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new ArgumentException($"{name} is required.");
    }
}

sealed record IssuePayload(int Number, string Body, DateTimeOffset CreatedAt, string CreatedBy)
{
    public static IssuePayload Read(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (root.TryGetProperty("event", out var eventElement) &&
            eventElement.TryGetProperty("issue", out var eventIssue))
        {
            root = eventIssue;
        }

        var number = root.GetProperty("number").GetInt32();
        var body = root.GetProperty("body").GetString() ?? "";
        var createdAtValue = root.GetProperty("created_at").GetString();
        if (!DateTimeOffset.TryParse(
                createdAtValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var createdAt))
        {
            throw new InvalidDataException("Issue created_at must be a valid ISO-8601 DateTimeOffset.");
        }

        var createdBy = root.GetProperty("user").GetProperty("login").GetString();
        if (string.IsNullOrWhiteSpace(createdBy))
        {
            throw new InvalidDataException("Issue user.login is required.");
        }

        return new IssuePayload(number, body, createdAt, createdBy);
    }
}

sealed record ValidatedBody(
    string? Organisation,
    string? GitHubHandle,
    string? Name,
    string? Email,
    List<string> InvalidReasons);

sealed record OnboardingBody(
    string? Organisation,
    [property: JsonPropertyName("githubHandle")]
    string? GitHubHandle,
    string? Name,
    string? Email);

sealed record ValidationResult(
    int Number,
    DateTimeOffset CreatedAt,
    DateTimeOffset DueDate,
    string CreatedBy,
    bool IsValid,
    IReadOnlyList<string> InvalidReasons,
    OnboardingBody Body);

static class ValidationConstants
{
    public static readonly HashSet<string> AllowedEmailDomains =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "gmail.com",
            "outlook.com",
            "outlook.kr",
            "hotmail.com",
            "naver.com",
            "kakao.com"
        };
}
