# Onboarding to Azure and GitHub

This repository is a reusable GitHub issue-form and Actions template for
onboarding people to:

- an Azure tenant, subscription, and security group; and
- a GitHub organization.

An applicant submits an issue form, the matching workflow validates the
request, performs the invitation, comments on the issue, applies labels, and
closes the issue.

## What this template includes

- English and Korean issue forms for Azure and GitHub onboarding.
- Separate workflows for Azure subscription and GitHub organization
  invitations.
- A local file-based C# validator that handles both request types and both
  languages.
- File-based C# scripts for Azure invitations, GitHub organization
  invitations, and initial service-principal setup.
- GitHub Copilot license pre-check guidance and images in the GitHub
  organization forms.

## How the onboarding flow works

```text
Issue form
    |
    v
Issue opened
    |
    +--> invite-user-to-azure.yml
    |        |
    |        +--> Validate-InvitationRequest.cs
    |        +--> Invite-ToAzure.cs
    |        +--> Comment, label, and close issue
    |
    +--> invite-user-to-github.yml
             |
             +--> Validate-InvitationRequest.cs
             +--> Invite-ToGitHubOrg.cs
             +--> Comment, label, and close issue
```

The workflows select a path from the issue title prefix. The validator accepts
English or Korean field headings and normalizes valid request types to their
English values before the workflow continues.

## Prerequisites

Before using this template, configure:

- A public or private GitHub repository with Actions enabled.
- An Azure subscription and tenant, if Azure onboarding is enabled.
- A GitHub organization, if GitHub onboarding is enabled.
- The .NET 10 SDK for local development and validation.
- Azure CLI (`az`) for Azure setup and invitation operations.
- GitHub CLI (`gh`) for repository configuration and GitHub API operations.
- An Azure service principal with the permissions required by the Azure
  invitation workflow.
- A GitHub App installed in the target repository and organization.

The GitHub App used by `invite-user-to-github.yml` must have the organization
permission **Members: write**. Without it, the organization invitation will
fail with HTTP 403.

## Configure a new repository from this template

1. Create a repository from this repository's template.
2. Update the organization names in:
   - `.github/ISSUE_TEMPLATE/invitation-request-azure-en.yml`
   - `.github/ISSUE_TEMPLATE/invitation-request-azure-ko.yml`
   - `.github/ISSUE_TEMPLATE/invitation-request-github-en.yml`
   - `.github/ISSUE_TEMPLATE/invitation-request-github-ko.yml`
   - the corresponding workflow files.
3. Update the title prefixes consistently in the issue forms, workflows, and
   `scripts/Validate-InvitationRequest.cs`.
4. Replace the Copilot guidance and images if the target program has different
   eligibility requirements.
5. Configure the repository variables and secrets described below.
6. Run the validation commands locally before enabling real invitations.

## GitHub configuration

The workflows use these repository variables:

| Name | Used by | Purpose |
| --- | --- | --- |
| `BOT_APP_ID` | Both workflows | GitHub App ID used to comment, label, and close issues |
| `AZURE_CLIENT_ID` | Azure workflow | Azure service-principal application ID |
| `AZURE_TENANT_ID` | Azure workflow | Azure tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Azure workflow | Azure subscription ID |
| `AZURE_SECURITY_GROUP` | Azure workflow | Security group receiving invited users |
| `INVITATION_DUE_DATE` | Both workflows | ISO 8601 deadline for submitted requests |

Add this repository secret:

| Name | Purpose |
| --- | --- |
| `BOT_PRIVATE_KEY` | Private key for the GitHub App |

The GitHub App must be installed on the repository. For the GitHub organization
workflow, it must also be installed in the target organization with
organization member write permission.

## Azure service-principal setup

Authenticate to the target Azure subscription and authenticate `gh` to the
target repository, then run:

```bash
dotnet run --file ./scripts/Setup-ServicePrincipal.cs -- \
  --app-name "invitation-automation" \
  --github-repo "OWNER/REPOSITORY"
```

The setup script:

- creates an Azure app registration and service principal;
- configures GitHub Actions OIDC for the `main` branch;
- grants the Microsoft Graph permissions required by the Azure invitation
  workflow;
- assigns the Contributor role on the subscription; and
- writes the Azure repository variables with `gh`.

Review these permissions for your environment before running the script. Use
the smallest scope and role that supports the operations you need.

## Local validation

Validate all YAML files:

```bash
for file in .github/ISSUE_TEMPLATE/*.yml .github/workflows/*.yml; do
  ruby -ryaml -e 'YAML.parse_file(ARGV[0])' "$file"
done
```

Run the validator against an issue payload:

```bash
dotnet run --file ./scripts/Validate-InvitationRequest.cs -- \
  --request-type github \
  --input ./payload.json \
  --output ./issue.json \
  --due-date "2026-12-31T23:59:59+09:00" \
  --organization "devrel-kr" \
  --github-output ./github-output
```

Use a mock payload when testing locally. Do not run invitation scripts against
production Azure or GitHub resources until the configuration and permissions
have been reviewed.

## Repository layout

| Path | Purpose |
| --- | --- |
| `.github/ISSUE_TEMPLATE/` | English and Korean onboarding forms |
| `.github/workflows/invite-user-to-azure.yml` | Azure invitation workflow |
| `.github/workflows/invite-user-to-github.yml` | GitHub organization invitation workflow |
| `scripts/Validate-InvitationRequest.cs` | Shared request validator |
| `scripts/Invite-ToAzure.cs` | Azure tenant invitation and group membership |
| `scripts/Invite-ToGitHubOrg.cs` | GitHub organization invitation |
| `scripts/Setup-ServicePrincipal.cs` | Azure OIDC and repository-variable setup |
| `images/` | Images used by the GitHub Copilot pre-check guidance |

## Security and operational notes

- Treat `BOT_PRIVATE_KEY` as a production credential and rotate it according
  to your organization's policy.
- Keep the Azure service principal and GitHub App permissions narrowly scoped.
- Review workflow changes carefully because they can send real invitations.
- Test with a dedicated organization, subscription, or controlled account
  before enabling the template for a larger program.
- The issue body is untrusted input. Preserve the existing environment-variable
  handoff to the validator rather than interpolating issue content into shell
  source.
- The default issue forms include a GitHub Copilot license pre-check. Remove or
  customize that section if it is not part of your program's eligibility rules.

## Customization checklist

When adapting this repository, review all of the following:

- organization and subscription names;
- issue-form title prefixes and field labels;
- allowed email domains in `Validate-InvitationRequest.cs`;
- invitation deadline and time zone;
- Azure security group;
- GitHub App permissions and installation;
- Azure service-principal permissions and scope;
- completion and invalid-request messages;
- labels used by the workflows; and
- images and program-specific eligibility guidance.

## License

This project is available under the [MIT License](LICENSE).
