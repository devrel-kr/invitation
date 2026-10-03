Param(
    [Parameter(Mandatory = $true)]
    [string] $Organization,

    [Parameter(Mandatory = $true)]
    [string] $GitHubHandle
)

$normalizedHandle = $GitHubHandle.Trim()

if ($normalizedHandle -notmatch '^[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,37}[a-zA-Z0-9])?$') {
    throw "GitHub handle '$GitHubHandle' is invalid."
}

Write-Output "Resolving GitHub user '$normalizedHandle'..."
$userId = gh api "users/$normalizedHandle" --jq '.id'
if (-not $userId) {
    throw "GitHub user '$normalizedHandle' was not found."
}

Write-Output "Inviting GitHub user '$normalizedHandle' ($userId) to organization '$Organization'..."
gh api --method POST "orgs/$Organization/invitations" -F invitee_id="$userId" -f role="direct_member"
if ($LASTEXITCODE -ne 0) {
    throw "Failed to invite GitHub user '$normalizedHandle' to organization '$Organization'."
}

Write-Output "User '$normalizedHandle' invited to organization '$Organization' successfully."
