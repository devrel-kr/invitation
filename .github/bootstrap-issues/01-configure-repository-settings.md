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
- [ ] Check for an existing `default` ruleset with `gh ruleset list --repo "{{ORG_NAME}}/{{REPOSITORY_NAME}}"`. If none exists, save this payload locally as `default-branch-ruleset.json` (do not commit it):

  ```json
  {
    "name": "default",
    "target": "branch",
    "enforcement": "active",
    "bypass_actors": [
      {
        "actor_id": null,
        "actor_type": "OrganizationAdmin",
        "bypass_mode": "always"
      },
      {
        "actor_id": 5,
        "actor_type": "RepositoryRole",
        "bypass_mode": "always"
      }
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

  Then create the ruleset with `gh api`:

  ```bash
  gh api --method POST "repos/{{ORG_NAME}}/{{REPOSITORY_NAME}}/rulesets" --input ./default-branch-ruleset.json
  ```
- [ ] Replace the quoted `{{ENTRA_TENANT_DOMAIN_NAME}}` placeholder in both Azure issue forms and `EXPECTED_ORGANIZATION` in `.github/workflows/onboard-user-to-azure.yml` with a verified domain from the target tenant. See [Configure the expected Entra tenant domain](https://github.com/{{ORG_NAME}}/{{REPOSITORY_NAME}}/blob/main/docs/configuration.md#configure-the-expected-entra-tenant-domain).
- [ ] Validate the issue forms and workflows before accepting onboarding requests.
