# [Bootstrap 1/5] Configure repository settings

Set the repository features, default-branch ruleset, and Azure tenant-domain configuration before setting up onboarding.

## Steps

- [ ] Review [Configure repository settings](https://github.com/{{ORG_NAME}}/{{REPOSITORY_NAME}}/blob/main/docs/configuration.md#configure-repository-settings).
- [ ] Authenticate `gh` as an organization owner or repository administrator.
- [ ] Apply the repository feature settings:

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
- [ ] Check for an existing `default` ruleset with `gh ruleset list --repo "{{ORG_NAME}}/{{REPOSITORY_NAME}}"`. If none exists, send the JSON payload directly to GitHub with `gh api`:

  ```bash
  gh api --method POST \
    "repos/{{ORG_NAME}}/{{REPOSITORY_NAME}}/rulesets" \
    --input - <<'JSON'
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
  JSON
  ```

  In PowerShell, use a here-string:

  ```powershell
  $rulesetJson = @'
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
  '@
  $rulesetJson | gh api --method POST "repos/{{ORG_NAME}}/{{REPOSITORY_NAME}}/rulesets" --input -
  ```
- [ ] Replace the quoted `{{ENTRA_TENANT_DOMAIN_NAME}}` placeholder in both Azure issue forms and `EXPECTED_ORGANIZATION` in `.github/workflows/onboard-user-to-azure.yml` with a verified domain from the target tenant. See [Configure the expected Entra tenant domain](https://github.com/{{ORG_NAME}}/{{REPOSITORY_NAME}}/blob/main/docs/configuration.md#configure-the-expected-entra-tenant-domain).

  From the repository root, in Bash on Linux, Codespaces, or Git Bash, change `contoso.onmicrosoft.com` below to the verified domain:

  ```bash
  set -euo pipefail
  tenant_domain="contoso.onmicrosoft.com"
  for file in \
    .github/ISSUE_TEMPLATE/onboarding-request-azure-en.yml \
    .github/ISSUE_TEMPLATE/onboarding-request-azure-ko.yml \
    .github/workflows/onboard-user-to-azure.yml; do
    if ! grep -Fq '{{ENTRA_TENANT_DOMAIN_NAME}}' "$file"; then
      printf 'Expected placeholder not found in %s\n' "$file" >&2
      exit 1
    fi
    sed -i "s|{{ENTRA_TENANT_DOMAIN_NAME}}|${tenant_domain}|g" "$file"
  done
  ```

  In PowerShell, change `$tenantDomain` to the verified domain:

  ```powershell
  $ErrorActionPreference = "Stop"
  $tenantDomain = "contoso.onmicrosoft.com"
  $placeholder = "{{ENTRA_TENANT_DOMAIN_NAME}}"
  $root = (Get-Location).ProviderPath
  $files = @(
      (Join-Path $root ".github/ISSUE_TEMPLATE/onboarding-request-azure-en.yml"),
      (Join-Path $root ".github/ISSUE_TEMPLATE/onboarding-request-azure-ko.yml"),
      (Join-Path $root ".github/workflows/onboard-user-to-azure.yml")
  )
  $encoding = [Text.UTF8Encoding]::new($false)

  foreach ($file in $files) {
      $content = [IO.File]::ReadAllText($file)
      if (-not $content.Contains($placeholder)) {
          throw "Expected placeholder not found in $file"
      }

      [IO.File]::WriteAllText(
          $file,
          $content.Replace($placeholder, $tenantDomain),
          $encoding
      )
  }
  ```
- [ ] Validate the issue forms and workflows before accepting onboarding requests.
