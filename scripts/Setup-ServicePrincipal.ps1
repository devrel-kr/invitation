Param(
    [Parameter(Mandatory = $true)]
    [string] $AppName,

    [Parameter(Mandatory = $true)]
    [string] $GitHubRepo
)

$branch = "main"

Write-Output "Retrieving current Azure subscription and tenant info..."
$subscriptionId = (az account show --query id -o tsv)
$tenantId = (az account show --query tenantId -o tsv)

# Create the app registration
Write-Output "Creating app registration '$AppName'..."
$app = az ad app create --display-name $AppName | ConvertFrom-Json
$appId = $app.appId
$objectId = $app.id

Write-Output "Created app registration: $AppName ($appId)"

# Create a service principal
Write-Output "Creating service principal for app '$AppName'..."
az ad sp create --id $appId

# Add federated credential for GitHub Actions (branch)
$federatedCredential = @{
    name        = "azure-invitation-github-actions-${branch}"
    issuer      = "https://token.actions.githubusercontent.com"
    subject     = "repo:${GitHubRepo}:ref:refs/heads/${branch}"
    audiences   = @("api://AzureADTokenExchange")
    description = "GitHub Actions OIDC for $GitHubRepo ($branch)"
} | ConvertTo-Json -Compress

$tempFile = [System.IO.Path]::GetTempFileName()
$federatedCredential | Out-File -FilePath $tempFile -Encoding utf-8 -Force

Write-Output "Creating federated credential for GitHub Actions OIDC..."
az ad app federated-credential create --id $objectId --parameters "@$tempFile"

Remove-Item $tempFile -Force

# Grant Microsoft Graph API permissions:
#   - User.Invite.All (09850681-111b-4a89-9bed-3f2cae46d706)
#   - GroupMember.ReadWrite.All (dbaae8cf-10b5-4b86-a4a1-f871c94c6695)
#   - Group.Read.All (5b567255-7703-4780-807c-7be8301ae99b)
$graphApiId = "00000003-0000-0000-c000-000000000000"

Write-Output "Adding Microsoft Graph API permissions (User.Invite.All, GroupMember.ReadWrite.All, Group.Read.All)..."
az ad app permission add --id $appId `
    --api $graphApiId `
    --api-permissions 09850681-111b-4a89-9bed-3f2cae46d706=Role `
                      dbaae8cf-10b5-4b86-a4a1-f871c94c6695=Role `
                      5b567255-7703-4780-807c-7be8301ae99b=Role

# Admin consent via Graph API appRoleAssignments
Write-Output "Granting admin consent for API permissions..."
$spId = az ad sp show --id $appId --query id -o tsv
$graphSpId = az ad sp show --id $graphApiId --query id -o tsv

$roleIds = @(
    "09850681-111b-4a89-9bed-3f2cae46d706",  # User.Invite.All
    "dbaae8cf-10b5-4b86-a4a1-f871c94c6695",  # GroupMember.ReadWrite.All
    "5b567255-7703-4780-807c-7be8301ae99b"    # Group.Read.All
)

foreach ($roleId in $roleIds) {
    $body = @{
        principalId = $spId
        resourceId  = $graphSpId
        appRoleId   = $roleId
    } | ConvertTo-Json -Compress

    $roleFile = [System.IO.Path]::GetTempFileName()
    $body | Out-File -FilePath $roleFile -Encoding utf-8 -Force

    az rest --method POST `
        --url "https://graph.microsoft.com/v1.0/servicePrincipals/$spId/appRoleAssignments" `
        --body "@$roleFile" `
        --headers "Content-Type=application/json"

    Remove-Item $roleFile -Force
}

# Assign Contributor role on the subscription
Write-Output "Assigning Contributor role on subscription '$subscriptionId'..."
az role assignment create `
    --assignee $appId `
    --role "Contributor" `
    --scope "/subscriptions/$subscriptionId"

# Save variables to the GitHub repository
Write-Output "Saving Azure variables to GitHub repository '$GitHubRepo'..."
gh variable set AZURE_CLIENT_ID --body $appId --repo $GitHubRepo
gh variable set AZURE_TENANT_ID --body $tenantId --repo $GitHubRepo
gh variable set AZURE_SUBSCRIPTION_ID --body $subscriptionId --repo $GitHubRepo

Write-Output ""
Write-Output "=== GitHub repository variables saved to $GitHubRepo ==="
Write-Output "AZURE_CLIENT_ID:       $appId"
Write-Output "AZURE_TENANT_ID:       $tenantId"
Write-Output "AZURE_SUBSCRIPTION_ID: $subscriptionId"
