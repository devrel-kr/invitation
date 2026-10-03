$script:AllowedEmailDomains = @(
    'gmail.com',
    'outlook.com',
    'outlook.kr',
    'hotmail.com',
    'naver.com',
    'kakao.com'
)

function Read-IssuePayload {
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    $payload = Get-Content $Path -Raw | ConvertFrom-Json

    # The workflow may capture either the whole GitHub context or the issue itself.
    if ($payload.event.issue) {
        return $payload.event.issue
    }

    return $payload
}

function Get-IssueFormValue {
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string] $Body,

        [Parameter(Mandatory = $true)]
        [string] $Label
    )

    $escapedLabel = [Regex]::Escape($Label)
    $match = [Regex]::Match(
        $Body,
        "(?ms)^###\s+$escapedLabel\s*\r?\n(.*?)(?=^###\s+|\z)"
    )

    if (-not $match.Success) {
        return $null
    }

    $value = $match.Groups[1].Value.Trim()
    if ($value -eq '_No response_') {
        return $null
    }

    return $value
}

function Test-GitHubHandle {
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory = $false)]
        [string] $Handle
    )

    return $Handle -match '^[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,37}[a-zA-Z0-9])?$'
}

function Test-GitHubProfileUrl {
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory = $false)]
        [string] $Url
    )

    if ([string]::IsNullOrWhiteSpace($Url) -eq $true) {
        return $false
    }

    if ([Uri]::IsWellFormedUriString($Url, [UriKind]::Absolute) -eq $false) {
        return $false
    }

    if ($Url.StartsWith('https://github.com/') -eq $false) {
        return $false
    }

    return $Url.Split('/', [StringSplitOptions]::RemoveEmptyEntries).Count -eq 3
}

function Test-AllowedEmail {
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory = $false)]
        [string] $Email
    )

    if ([string]::IsNullOrWhiteSpace($Email) -eq $true) {
        return $false
    }

    $address = $null
    if ([System.Net.Mail.MailAddress]::TryCreate($Email, [ref] $address) -eq $false) {
        return $false
    }

    return $script:AllowedEmailDomains -contains $address.Host
}

function ConvertFrom-IsoDate {
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory = $true)]
        [object] $Value
    )

    # ConvertFrom-Json silently turns ISO-8601 strings into [datetime] values. Coercing
    # those back to a string drops the Kind, so "...Z" would be re-parsed as local time
    # and shift the instant by the host's UTC offset. Convert by type instead.
    if ($Value -is [DateTimeOffset]) {
        return $Value
    }

    if ($Value -is [datetime]) {
        return [DateTimeOffset]::new($Value)
    }

    return [DateTimeOffset]::Parse(
        [string] $Value,
        [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::RoundtripKind
    )
}

function Write-InvitationResult {
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory = $true)]
        [object] $Issue,

        [Parameter(Mandatory = $true)]
        [string] $DueDate,

        [Parameter(Mandatory = $true)]
        [hashtable] $Body,

        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [System.Collections.Generic.List[string]] $InvalidReasons,

        [Parameter(Mandatory = $true)]
        [string] $AuthorMismatchReason,

        [Parameter(Mandatory = $true)]
        [string] $OutputFile
    )

    $createdAt = ConvertFrom-IsoDate -Value $Issue.created_at
    $dueBy = ConvertFrom-IsoDate -Value $DueDate

    if ($createdAt -gt $dueBy) {
        $InvalidReasons.Add('제출 마감기한이 지났습니다.')
    }

    if ($Body.githubHandle -and $Body.githubHandle -ne $Issue.user.login) {
        $InvalidReasons.Add($AuthorMismatchReason)
    }

    $result = @{
        number = $Issue.number
        createdAt = $createdAt
        dueDate = $dueBy
        createdBy = $Issue.user.login
        isValid = $InvalidReasons.Count -eq 0
        invalidReasons = @($InvalidReasons)
        body = $Body
    }

    $result | ConvertTo-Json -Depth 5 | Out-File -FilePath $OutputFile -Encoding utf8
}

Export-ModuleMember -Function @(
    'Read-IssuePayload',
    'Get-IssueFormValue',
    'Test-GitHubHandle',
    'Test-GitHubProfileUrl',
    'Test-AllowedEmail',
    'ConvertFrom-IsoDate',
    'Write-InvitationResult'
)
