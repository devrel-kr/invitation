Param(
    [Parameter(Mandatory = $true)]
    [string] $Email,

    [Parameter(Mandatory = $true)]
    [string] $Name,

    [Parameter(Mandatory = $true)]
    [string] $SecurityGroup
)

# Invite external user to the tenant
$body = @{
    invitedUserEmailAddress = $Email
    inviteRedirectUrl       = "https://portal.azure.com"
    sendInvitationMessage   = $true
    invitedUserDisplayName  = $Name
} | ConvertTo-Json -Compress

$tempFile = [System.IO.Path]::GetTempFileName()
$body | Out-File -FilePath $tempFile -Encoding utf-8 -Force

$invitation = az rest --method POST `
    --url "https://graph.microsoft.com/v1.0/invitations" `
    --body "@$tempFile" `
    --headers "Content-Type=application/json" | ConvertFrom-Json

Remove-Item $tempFile -Force

if (-not $invitation.invitedUser.id) {
    Write-Error "Failed to invite user '$Email'"
    exit 1
}

$userId = $invitation.invitedUser.id
Write-Output "Invited user ID: $userId"

# Get the security group ID
$groupId = az ad group show --group $SecurityGroup --query id -o tsv
if (-not $groupId) {
    Write-Error "Security group '$SecurityGroup' not found"
    exit 1
}

# Add the invited user to the security group
az ad group member add --group $groupId --member-id $userId

Write-Output "User '$Email' invited and added to security group '$SecurityGroup' successfully."
