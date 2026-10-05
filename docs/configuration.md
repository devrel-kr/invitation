# Configuration guide

This guide explains how to provision the identities and GitHub Actions settings used by the onboarding workflows.

## Configuration overview

Configure the values in **Repository settings → Secrets and variables → Actions**.

| Name | Type | Provisioning method |
| --- | --- | --- |
| `BOT_APP_ID` | Repository variable | Copy from the GitHub App settings page |
| `BOT_PRIVATE_KEY` | Repository secret | Generate and download from the GitHub App settings page |
| `AZURE_CLIENT_ID` | Repository variable | Written by `Setup-ServicePrincipal.cs` |
| `AZURE_TENANT_ID` | Repository variable | Written by `Setup-ServicePrincipal.cs` |
| `AZURE_SUBSCRIPTION_ID` | Repository variable | Written by `Setup-ServicePrincipal.cs` |
| `AZURE_SECURITY_GROUP` | Repository variable | Set to an existing Microsoft Entra security group |
| `INVITATION_DUE_DATE` | Repository variable | Set manually as an ISO 8601 timestamp |

## Create the GitHub App

The workflows use a GitHub App instead of the repository's default `GITHUB_TOKEN` because the generated installation token can operate across the repository and its owning organization. The app comments on request issues, changes labels, closes completed requests, and sends GitHub organization invitations.

1. In the target GitHub organization, open **Settings → Developer settings → GitHub Apps**, and select **New GitHub App**. See [Registering a GitHub App](https://docs.github.com/apps/creating-github-apps/registering-a-github-app/registering-a-github-app).
2. Enter a name and homepage URL. A callback URL is not required.
3. Clear **Active** under **Webhook** because these workflows are triggered by repository issues, not GitHub App webhooks.
4. Configure these permissions:

   | Scope | Permission | Reason |
   | --- | --- | --- |
   | Repository permissions → Contents | Read-only | Allows the generated token to access repository content |
   | Repository permissions → Issues | Read and write | Allows comments, labels, issue lookup, and issue closure |
   | Organization permissions → Members | Read and write | Allows `Invite-ToGitHubOrg.cs` to invite organization members |

5. Create the app, then copy its **App ID**.
6. Under **Private keys**, select **Generate a private key** and securely save the downloaded PEM file.
7. Install the app on the target organization. Grant it access to the repository created from this template.

The organization must own or install the app for the organization-level `Members: write` permission to be available. A missing permission or installation causes the GitHub invitation request to fail with HTTP 403.

Add the App ID as a repository variable:

```bash
gh variable set BOT_APP_ID --body "YOUR_APP_ID" --repo "OWNER/REPOSITORY"
```

Add the PEM file as a repository secret:

```bash
gh secret set BOT_PRIVATE_KEY < ./path/to/private-key.pem --repo "OWNER/REPOSITORY"
```

Do not commit the private key. Delete any unnecessary local copy after storing it in an approved secret manager and GitHub Actions.

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

## Set the Azure security group

`AZURE_SECURITY_GROUP` identifies the Microsoft Entra security group that receives each invited user. `Invite-ToAzure.cs` resolves the value with `az ad group show` and then adds the invited user as a member.

Find the target group:

```bash
az ad group list --query "[].{name:displayName,id:id}" --output table
```

If the group does not exist, create a dedicated security group:

```bash
az ad group create \
  --display-name "invitation-participants" \
  --mail-nickname "invitation-participants" \
  --description "Users onboarded by the invitation workflow"
```

Set the variable to the group's unique display name or object ID:

```bash
gh variable set AZURE_SECURITY_GROUP \
  --body "GROUP_NAME_OR_OBJECT_ID" \
  --repo "OWNER/REPOSITORY"
```

Use a dedicated group whose access assignments match the intended onboarding scope. The automation identity needs permission to read the group and update its membership.

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

Confirm that all seven names from the configuration overview are present. GitHub does not reveal secret values after they are stored.

Before accepting real requests:

1. Verify that the GitHub App is installed on the repository and organization.
2. Verify that the app has `Issues: write` and organization `Members: write`.
3. Open the Azure app registration and verify its federated credential and Microsoft Graph permissions.
4. Confirm that the Azure security group exists and the automation identity can update it.
5. Run each workflow manually with a controlled test issue.

Review workflow logs for authentication or permission errors, and remove test invitations when finished.
