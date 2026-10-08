# Configuration guide

This guide explains how to provision the identities and GitHub Actions settings used by the onboarding workflows.

## Configuration overview

Configure the values in **Repository settings → Secrets and variables → Actions**.

| Name | Type | Provisioning method |
| --- | --- | --- |
| `BOT_APP_ID` | Repository variable | Written by `Setup-GitHubApp.cs` |
| `BOT_PRIVATE_KEY` | Repository secret | Written by `Setup-GitHubApp.cs` |
| `AZURE_CLIENT_ID` | Repository variable | Written by `Setup-ServicePrincipal.cs` |
| `AZURE_TENANT_ID` | Repository variable | Written by `Setup-ServicePrincipal.cs` |
| `AZURE_SUBSCRIPTION_ID` | Repository variable | Written by `Setup-ServicePrincipal.cs` |
| `AZURE_SECURITY_GROUP` | Repository variable | Written by `Setup-EntraSecurityGroup.cs` |
| `GITHUB_TEAM_ID` | Repository variable | Written by `Setup-GitHubTeam.cs` |
| `INVITATION_DUE_DATE` | Repository variable | Set manually as an ISO 8601 timestamp |

## Create the GitHub App

The workflows use a GitHub App instead of the repository's default `GITHUB_TOKEN` because the generated installation token can operate across the repository and its owning organization. The app comments on request issues, changes labels, closes completed requests, and sends GitHub organization invitations.

`Setup-GitHubApp.cs` uses GitHub's [App Manifest flow](https://docs.github.com/apps/sharing-github-apps/registering-a-github-app-from-a-manifest). The script opens a browser for organization-owner approval, receives the temporary callback code on localhost, exchanges it for the App ID and one-time private key, and writes `BOT_APP_ID` and `BOT_PRIVATE_KEY` to the repository.

Authenticate `gh`, then run:

```bash
gh auth login

dotnet run --file ./scripts/Setup-GitHubApp.cs -- \
  --app-name "invitation-automation" \
  --github-org "OWNER" \
  --github-repo "OWNER/REPOSITORY"
```

The script listens on `http://127.0.0.1:53682/` for up to 10 minutes. Run it from a machine where that address can be opened in your browser, or forward the port when using a remote development environment. Use `--callback-port` to select another local port.

Use `--no-open` when the browser must be opened manually, such as in a remote shell.

The manifest requests these permissions:

   | Scope | Permission | Reason |
   | --- | --- | --- |
   | Repository permissions → Contents | Read-only | Allows the generated token to access repository content |
   | Repository permissions → Issues | Read and write | Allows comments, labels, issue lookup, and issue closure |
   | Organization permissions → Members | Read and write | Allows `Onboard-ToGitHub.cs` to invite members into the configured team |

After registration, the script opens the app installation page. Install the app on the target organization and grant it access to the repository created from this template. App installation still requires organization-owner approval and cannot be completed by the manifest exchange alone.

The organization must own or install the app for the organization-level `Members: write` permission to be available. A missing permission or installation causes the GitHub invitation request to fail with HTTP 403. The private key is sent directly to `gh secret set` through standard input and is not written to disk by the script.

## Create the GitHub onboarding team

Every GitHub organization invitation must include a team. `Setup-GitHubTeam.cs` searches all visible organization teams for an exact name match, reuses the existing team when found, or creates a new team when absent. It then writes the numeric team ID to `GITHUB_TEAM_ID`.

The authenticated GitHub user must be an organization member allowed to create teams and must be able to write Actions variables in the repository. Organization owners can restrict team creation to owners.

```bash
dotnet run --file ./scripts/Setup-GitHubTeam.cs -- \
  --github-org "OWNER" \
  --team-name "invitation-participants" \
  --description "Users onboarded by the invitation workflow." \
  --privacy "closed" \
  --github-repo "OWNER/REPOSITORY"
```

The supported privacy values are `closed` and `secret`. On each successful invitation, `Onboard-ToGitHub.cs` sends the configured team ID in the `team_ids` array, so GitHub adds the new member to that team after they accept the organization invitation.

## Create the Azure workload identity

The Azure workflow uses OpenID Connect (OIDC) to exchange GitHub's short-lived identity token for an Azure access token. It does not require an Azure client secret.

For background on this authentication model, see [Use the Azure Login action with OpenID Connect](https://learn.microsoft.com/azure/developer/github/connect-from-azure-openid-connect).

The included setup script creates:

- a Microsoft Entra app registration and service principal;
- a federated credential restricted to the repository's `main` branch;
- Microsoft Graph application permissions for `User.Invite.All`, `GroupMember.ReadWrite.All`, and `Group.Read.All`;
- admin-consented app-role assignments for those permissions;
- an Azure `Contributor` role assignment at subscription scope; and
- the `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID` repository variables.

The Graph permissions allow the workflow to invite an external user, find the configured group, and add the invited user to it. The Azure IDs tell `azure/login` which workload identity, tenant, and subscription to use.

### Prerequisites

Before running the script:

1. Install the .NET 10 SDK, Azure CLI, and GitHub CLI.
2. Sign in to Azure and select the target subscription:

   ```bash
   az login
   az account set --subscription "SUBSCRIPTION_ID_OR_NAME"
   ```

3. Sign in to GitHub:

   ```bash
   gh auth login
   ```

4. Confirm that your Azure account can create app registrations, grant tenant-wide admin consent, and create role assignments at the subscription scope.
5. Confirm that your GitHub account can write Actions variables in the target repository.

Run the setup script:

```bash
dotnet run --file ./scripts/Setup-ServicePrincipal.cs -- \
  --app-name "invitation-automation" \
  --github-repo "OWNER/REPOSITORY"
```

The federated credential created by the script trusts only:

```text
repo:OWNER/REPOSITORY:ref:refs/heads/main
```

If the workflow runs from a different branch, update the `Branch` constant in `Setup-ServicePrincipal.cs` before running it, or create an additional federated credential with the required subject.

Review the generated permissions for your environment. In particular, the setup script currently grants `Contributor` at subscription scope. Replace it with a narrower custom role or scope if your invitation process does not require that level of Azure resource access.

## Create the Azure security group

`AZURE_SECURITY_GROUP` identifies the Microsoft Entra security group that receives each invited user. `Onboard-ToAzure.cs` resolves the value with `az ad group show` and then adds the invited user as a member.

`Setup-EntraSecurityGroup.cs` searches for an exact display-name match, reuses an existing security-enabled group, or creates a new security group. It fails when duplicate display names make the result ambiguous or when the existing group is not security-enabled. The script stores the group object ID in `AZURE_SECURITY_GROUP`.

```bash
dotnet run --file ./scripts/Setup-EntraSecurityGroup.cs -- \
  --group-name "invitation-participants" \
  --description "Users onboarded by the invitation workflow." \
  --github-repo "OWNER/REPOSITORY"
```

The script derives a mail nickname from the display name. Use `--mail-nickname` to provide one explicitly. Use a dedicated group whose access assignments match the intended onboarding scope. The automation identity needs permission to read the group and update its membership.

## Set the invitation deadline

`INVITATION_DUE_DATE` is the last accepted submission time. Both workflows pass it to the shared validator, which rejects requests submitted after the deadline.

Use an ISO 8601 timestamp with an explicit UTC offset:

```bash
gh variable set INVITATION_DUE_DATE \
  --body "2026-12-31T23:59:59+09:00" \
  --repo "OWNER/REPOSITORY"
```

An explicit offset avoids interpreting the deadline in the runner's local time zone.

## Verify the configuration

List the configured variables and secret names:

```bash
gh variable list --repo "OWNER/REPOSITORY"
gh secret list --repo "OWNER/REPOSITORY"
```

Confirm that all eight names from the configuration overview are present. GitHub does not reveal secret values after they are stored.

Before accepting real requests:

1. Verify that the GitHub App is installed on the repository and organization.
2. Verify that the app has `Issues: write` and organization `Members: write`.
3. Confirm that `GITHUB_TEAM_ID` identifies the intended organization team.
4. Open the Azure app registration and verify its federated credential and Microsoft Graph permissions.
5. Confirm that `AZURE_SECURITY_GROUP` identifies the intended security group and that the automation identity can update it.
6. Run each workflow manually with a controlled test issue.

Review workflow logs for authentication or permission errors, and remove test invitations when finished.
