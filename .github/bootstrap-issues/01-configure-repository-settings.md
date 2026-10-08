# [Bootstrap 1/5] Configure repository settings

Set the repository features, default-branch ruleset, and Azure tenant-domain configuration before setting up onboarding.

## Steps

- [ ] Review [Configure repository settings](https://github.com/{{ORG_NAME}}/{{REPOSITORY_NAME}}/blob/main/docs/configuration.md#configure-repository-settings).
- [ ] Authenticate `gh` as an organization owner or repository administrator.
- [ ] Apply the repository feature settings:

  ```bash
  # zsh/bash
  gh repo edit "{{ORG_NAME}}/{{REPOSITORY_NAME}}" \
    --enable-wiki=false \
    --enable-discussions=false \
    --enable-projects=false \
    --enable-issues \
    --enable-squash-merge \
    --enable-merge-commit=false \
    --enable-rebase-merge=false
  ```

  ```powershell
  # PowerShell
  gh repo edit "{{ORG_NAME}}/{{REPOSITORY_NAME}}" `
    --enable-wiki=false `
    --enable-discussions=false `
    --enable-projects=false `
    --enable-issues `
    --enable-squash-merge `
    --enable-merge-commit=false `
    --enable-rebase-merge=false
  ```

  This leaves repository visibility and pull-request availability unchanged; pull requests can be merged only with squash commits.
- [ ] Create the default-branch ruleset. The helper checks for an existing `default` ruleset and skips creation if one is already present; verify that an existing ruleset matches the requested settings.

  ```bash
  dotnet run --file ./scripts/Configure-DefaultBranchRuleset.cs -- --repo "{{ORG_NAME}}/{{REPOSITORY_NAME}}"
  ```

  The helper uses the authenticated `gh` session. If running it outside the repository root, provide the path to the script.
- [ ] Configure the verified Entra tenant domain by replacing the example domain and running this command. From another directory, provide the path to the helper script.

  ```bash
  dotnet run --file ./scripts/Configure-EntraTenantDomain.cs -- --tenant-domain "contoso.onmicrosoft.com"
  ```

  See [Configure the expected Entra tenant domain](https://github.com/{{ORG_NAME}}/{{REPOSITORY_NAME}}/blob/main/docs/configuration.md#configure-the-expected-entra-tenant-domain) for details.
- [ ] Validate the issue forms and workflows before accepting onboarding requests.
