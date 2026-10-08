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
- [ ] Create the `default` ruleset for the default branch by following [Create the default-branch ruleset](https://github.com/{{ORG_NAME}}/{{REPOSITORY_NAME}}/blob/main/docs/configuration.md#create-the-default-branch-ruleset).
- [ ] Replace the quoted `{{ENTRA_TENANT_DOMAIN_NAME}}` placeholder in both Azure issue forms and `EXPECTED_ORGANIZATION` in `.github/workflows/onboard-user-to-azure.yml` with a verified domain from the target tenant. See [Configure the expected Entra tenant domain](https://github.com/{{ORG_NAME}}/{{REPOSITORY_NAME}}/blob/main/docs/configuration.md#configure-the-expected-entra-tenant-domain).
- [ ] Validate the issue forms and workflows before accepting onboarding requests.
