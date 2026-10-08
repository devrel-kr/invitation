# Configuration guide

This guide explains how to provision the identities and GitHub Actions settings used by the onboarding workflows.

## Configuration overview

Configure the values in **Repository settings → Secrets and variables → Actions**.

| Name                      | Type                         | Provisioning method                                                                 |
| ------------------------- | ---------------------------- | ----------------------------------------------------------------------------------- |
| `APP_CLIENT_ID`           | Repository variable          | Written by `Setup-GitHubApp.cs`                                                     |
| `APP_PRIVATE_KEY`         | Repository secret            | Written by `Setup-GitHubApp.cs`                                                     |
| `AZURE_CLIENT_ID`         | Repository variable          | Written by `Setup-ServicePrincipal.cs`                                              |
| `AZURE_TENANT_ID`         | Repository variable          | Written by `Setup-ServicePrincipal.cs`                                              |
| `AZURE_SUBSCRIPTION_ID`   | Repository variable          | Written by `Setup-ServicePrincipal.cs`                                              |
| `AZURE_SECURITY_GROUP_ID` | Repository variable          | Written by `Setup-EntraSecurityGroup.cs`                                            |
| `GITHUB_TEAM_ID`          | Repository variable          | Written by `Setup-GitHubTeam.cs`                                                    |
| `ONBOARDING_DUE_DATE`     | Optional repository variable | Set an ISO 8601 timestamp to enforce a deadline; leave unset for ongoing onboarding |

## Initialize a repository from this template

When a repository is created from this template, `.github/workflows/init.yml` runs on the initial default-branch creation event. It skips the template repository and runs only on the generated repository's default branch. The initializer:

- replaces repository-owner and repository-name placeholders in the README, configuration documentation, issue forms, onboarding workflows, C# scripts, and bootstrap issue bodies;
- creates the numbered setup issues from `.github/bootstrap-issues` in order; and
- commits the personalized files, then removes `init.yml` and the bootstrap issue source files.

The workflow can be manually dispatched from the default branch if the initial run needs recovery and the initializer is still present. It only personalizes files and creates setup issues; it does not create identities or configure repository variables or secrets. It also does not create repository rulesets: its `GITHUB_TOKEN` can write repository contents and issues, but cannot administer repository settings. Bootstrap issue 1 creates the ruleset manually using an administrator-authenticated `gh` session. Complete the generated issues in order before accepting onboarding requests.

The Entra tenant domain cannot be inferred from GitHub repository metadata, so the initializer leaves that value for manual configuration in Bootstrap issue 1. See the matching section below and complete it before accepting Azure onboarding requests.

## Configure repository settings

**Bootstrap issue 1/5**

Use `gh` as a repository administrator or organization owner to configure the repository features:

```bash
gh repo edit "{{ORG_NAME}}/{{REPOSITORY_NAME}}" \
  --enable-wiki=false \
  --enable-discussions=false \
  --enable-projects=false \
  --enable-issues \
  --enable-squash-merge \
  --enable-merge-commit=false \
  --enable-rebase-merge=false
```

This leaves repository visibility and pull-request availability unchanged; pull requests can be merged only with squash commits.

### Create the default-branch ruleset

The initializer does not create rulesets because its `GITHUB_TOKEN` cannot administer repository settings. Create this ruleset with an administrator-authenticated `gh` session. It targets the repository's default branch, prevents deletion and force pushes, and lets organization administrators and repository administrators bypass those rules. The `OrganizationAdmin` bypass applies only to organization-owned repositories.

Check for an existing ruleset named `default` before creating another:

```bash
gh ruleset list --repo "{{ORG_NAME}}/{{REPOSITORY_NAME}}"
```

Save this payload locally as `default-branch-ruleset.json` (do not commit it):

```json
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
```

`RepositoryRole` actor ID `5` represents repository administrators. Create the ruleset with `gh api`:

The payload is also included in Bootstrap issue 1 so it can be copied directly from the task; keep both copies aligned.

```bash
gh api --method POST \
  "repos/{{ORG_NAME}}/{{REPOSITORY_NAME}}/rulesets" \
  --input ./default-branch-ruleset.json
```

### Configure the expected Entra tenant domain

The Azure validator checks that the submitted organization matches the configured tenant domain. Use a verified domain from the same tenant as the service principal; this is different from the tenant ID. Replace the quoted `{{ENTRA_TENANT_DOMAIN_NAME}}` value in both Azure issue forms and `EXPECTED_ORGANIZATION` in `.github/workflows/onboard-user-to-azure.yml` with the same domain.

## Create the GitHub App

**Bootstrap issue 2/5**

The workflows use a GitHub App instead of the repository's default `GITHUB_TOKEN` because the generated installation token can operate across the repository and its owning organization. The app comments on onboarding issues, changes labels, closes completed requests, and manages GitHub organization membership.

`Setup-GitHubApp.cs` uses GitHub's [App Manifest flow](https://docs.github.com/apps/sharing-github-apps/registering-a-github-app-from-a-manifest). The script opens a browser for organization-owner approval, receives the temporary callback code on localhost, exchanges it for the App client ID and one-time private key, and writes `APP_CLIENT_ID` and `APP_PRIVATE_KEY` to the repository.

`--app-name` sets the GitHub App's name. `onboarding-automation` is an example; replace it with the name you choose. The Azure app registration created in Bootstrap issue 4 is a separate resource and can have a different name.

Authenticate `gh`, then run:

```bash
# zsh/bash
gh auth login

dotnet run --file ./scripts/Setup-GitHubApp.cs -- \
  --app-name "onboarding-automation" \
  --github-org "{{ORG_NAME}}" \
  --github-repo "{{ORG_NAME}}/{{REPOSITORY_NAME}}"
```

```powershell
# PowerShell
gh auth login

dotnet run --file ./scripts/Setup-GitHubApp.cs -- `
  --app-name "onboarding-automation" `
  --github-org "{{ORG_NAME}}" `
  --github-repo "{{ORG_NAME}}/{{REPOSITORY_NAME}}"
```

The script listens on `http://127.0.0.1:53682/` for up to 10 minutes. On a local machine, it registers a loopback callback URL with GitHub, so run the script on the same machine as the browser.

In GitHub Codespaces, the script detects `CODESPACES=true` and registers `https://<codespace-name>-<port>.<forwarding-domain>/callback/` as the callback while continuing to listen on loopback. The default dev container forwards port `53682`. Run the setup command with `--no-open`, then open the forwarded registration URL printed by the script in a browser signed in to GitHub. Keep the port private so only your Codespaces user can access it. Use `--callback-port` to select another port; Codespaces will forward that port when the script prints its local listener URL.

`--no-open` prevents the script from launching a browser. In Codespaces, the script always prints the forwarded URL for you to open manually.

The manifest requests these permissions:

| Scope                              | Permission     | Reason                                                             |
| ---------------------------------- | -------------- | ------------------------------------------------------------------ |
| Repository permissions → Contents  | Read-only      | Allows the generated token to access repository content            |
| Repository permissions → Issues    | Read and write | Allows comments, labels, issue lookup, and issue closure           |
| Organization permissions → Members | Read and write | Allows `Onboard-ToGitHub.cs` to add members to the configured team |

After registration, the script opens the app installation page. Install the app on the target organization and grant it access to the repository created from this template. App installation still requires organization-owner approval and cannot be completed by the manifest exchange alone.

The organization must own or install the app for the organization-level `Members: write` permission to be available. A missing permission or installation causes GitHub onboarding to fail with HTTP 403. The private key is sent directly to `gh secret set` through standard input and is not written to disk by the script.

## Create the GitHub onboarding team

**Bootstrap issue 3/5**

GitHub organization onboarding assigns each new member to a team. `Setup-GitHubTeam.cs` searches all visible organization teams for an exact name match, reuses the existing team when found, or creates a new team when absent. It then writes the numeric team ID to `GITHUB_TEAM_ID`.

The authenticated GitHub user must be an organization member allowed to create teams and must be able to write Actions variables in the repository. Organization owners can restrict team creation to owners.

`--team-name` is the name of the team to reuse or create. `onboarding-participants` is an example; replace it with the team name your organization wants to use.

```bash
# zsh/bash
dotnet run --file ./scripts/Setup-GitHubTeam.cs -- \
  --github-org "{{ORG_NAME}}" \
  --team-name "onboarding-participants" \
  --description "Users onboarded by this workflow." \
  --privacy "closed" \
  --github-repo "{{ORG_NAME}}/{{REPOSITORY_NAME}}"
```

```powershell
# PowerShell
dotnet run --file ./scripts/Setup-GitHubTeam.cs -- `
  --github-org "{{ORG_NAME}}" `
  --team-name "onboarding-participants" `
  --description "Users onboarded by this workflow." `
  --privacy "closed" `
  --github-repo "{{ORG_NAME}}/{{REPOSITORY_NAME}}"
```

The supported privacy values are `closed` and `secret`. On each successful onboarding, `Onboard-ToGitHub.cs` sends the configured team ID in the `team_ids` array, so GitHub adds the new member to that team after they complete the organization membership flow.

## Create the Azure workload identity

**Bootstrap issue 4/5**

The Azure workflow uses OpenID Connect (OIDC) to exchange GitHub's short-lived identity token for an Azure access token. It does not require an Azure client secret.

For background on this authentication model, see [Use the Azure Login action with OpenID Connect](https://learn.microsoft.com/azure/developer/github/connect-from-azure-openid-connect).

The included setup script creates:

- a Microsoft Entra app registration and service principal;
- a federated credential restricted to the repository's `main` branch;
- Microsoft Graph application permissions for `User.Invite.All`, `GroupMember.ReadWrite.All`, and `Group.Read.All`;
- admin-consented app-role assignments for those permissions;
- an Azure `Contributor` role assignment at subscription scope; and
- the `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID` repository variables.

The Graph permissions allow the workflow to onboard an external user, find the configured group, and add the user to it. The Azure IDs tell `azure/login` which workload identity, tenant, and subscription to use.

`--app-name` sets the Microsoft Entra app registration's display name. `onboarding-automation` is an example; replace it with a descriptive name you choose. This app registration is separate from the GitHub App created in Bootstrap issue 2.

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
# zsh/bash
dotnet run --file ./scripts/Setup-ServicePrincipal.cs -- \
  --app-name "onboarding-automation" \
  --github-repo "{{ORG_NAME}}/{{REPOSITORY_NAME}}"
```

```powershell
# PowerShell
dotnet run --file ./scripts/Setup-ServicePrincipal.cs -- `
  --app-name "onboarding-automation" `
  --github-repo "{{ORG_NAME}}/{{REPOSITORY_NAME}}"
```

The federated credential created by the script trusts only:

```text
repo:{{ORG_NAME}}/{{REPOSITORY_NAME}}:ref:refs/heads/main
```

If the workflow runs from a different branch, update the `Branch` constant in `Setup-ServicePrincipal.cs` before running it, or create an additional federated credential with the required subject.

Review the generated permissions for your environment. In particular, the setup script currently grants `Contributor` at subscription scope. Replace it with a narrower custom role or scope if your onboarding process does not require that level of Azure resource access.

## Create the Azure security group

**Bootstrap issue 5/5**

`AZURE_SECURITY_GROUP_ID` identifies the Microsoft Entra security group that receives each onboarded user. `Onboard-ToAzure.cs` resolves the value with `az ad group show` and then adds the user as a member.

`Setup-EntraSecurityGroup.cs` uses `--group-name` as the group's display name. `onboarding-participants` is an example; replace it with your organization's chosen name. The script reuses a matching security-enabled group or creates one if none exists. It fails when duplicate display names make the result ambiguous or when the existing group is not security-enabled. The script stores the group object ID in `AZURE_SECURITY_GROUP_ID`.

```bash
# zsh/bash
dotnet run --file ./scripts/Setup-EntraSecurityGroup.cs -- \
  --group-name "onboarding-participants" \
  --description "Users onboarded by this workflow." \
  --github-repo "{{ORG_NAME}}/{{REPOSITORY_NAME}}"
```

```powershell
# PowerShell
dotnet run --file ./scripts/Setup-EntraSecurityGroup.cs -- `
  --group-name "onboarding-participants" `
  --description "Users onboarded by this workflow." `
  --github-repo "{{ORG_NAME}}/{{REPOSITORY_NAME}}"
```

The script derives a mail nickname from the display name. Use `--mail-nickname` to provide one explicitly. Use a dedicated group whose access assignments match the intended onboarding scope. The automation identity needs permission to read the group and update its membership.

### Optional onboarding deadline

`ONBOARDING_DUE_DATE` is optional. When set, it is the last accepted submission time, and both workflows pass it to the shared validator to reject later requests. When it is unset or empty, no submission deadline is enforced and invalid-request comments omit the due-date line. A non-empty value that is not a valid ISO 8601 timestamp causes validation to fail.

For ongoing onboarding, leave the variable unset. To remove a previously configured deadline:

```bash
# zsh/bash
gh variable delete ONBOARDING_DUE_DATE \
  --repo "{{ORG_NAME}}/{{REPOSITORY_NAME}}"
```

```powershell
# PowerShell
gh variable delete ONBOARDING_DUE_DATE `
  --repo "{{ORG_NAME}}/{{REPOSITORY_NAME}}"
```

Use an ISO 8601 timestamp with an explicit UTC offset:

```bash
# zsh/bash
gh variable set ONBOARDING_DUE_DATE \
  --body "2026-12-31T23:59:59+09:00" \
  --repo "{{ORG_NAME}}/{{REPOSITORY_NAME}}"
```

```powershell
# PowerShell
gh variable set ONBOARDING_DUE_DATE \
  --body "2026-12-31T23:59:59+09:00" \
  --repo "{{ORG_NAME}}/{{REPOSITORY_NAME}}"
```

An explicit offset avoids interpreting the deadline in the runner's local time zone.

## Verify the configuration

List the configured variables and secret names:

```bash
gh variable list --repo "{{ORG_NAME}}/{{REPOSITORY_NAME}}"
gh secret list --repo "{{ORG_NAME}}/{{REPOSITORY_NAME}}"
```

Confirm that all required values from the configuration overview are present. `ONBOARDING_DUE_DATE` is optional; it should be set only when you want requests to expire. GitHub does not reveal secret values after they are stored.

Before accepting real requests:

1. Verify that the GitHub App is installed on the repository and organization.
2. Verify that the app has `Issues: write` and organization `Members: write`.
3. Confirm that `GITHUB_TEAM_ID` identifies the intended organization team.
4. Open the Azure app registration and verify its federated credential and Microsoft Graph permissions.
5. Confirm that `AZURE_SECURITY_GROUP_ID` identifies the intended security group and that the automation identity can update it.
6. Run each workflow manually with a controlled test issue.

Review workflow logs for authentication or permission errors, and remove test accounts when finished.
