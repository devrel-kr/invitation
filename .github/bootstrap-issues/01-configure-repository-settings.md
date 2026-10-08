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
    --enable-issues
  ```

  This leaves repository visibility, pull-request availability, and PR merge settings unchanged.
- [ ] Create the `default` ruleset for the default branch by following [Create the default-branch ruleset](https://github.com/{{ORG_NAME}}/{{REPOSITORY_NAME}}/blob/main/docs/configuration.md#create-the-default-branch-ruleset).
- [ ] Replace the quoted `{{ENTRA_TENANT_DOMAIN_NAME}}` placeholder in both Azure issue forms and `EXPECTED_ORGANIZATION` in `.github/workflows/onboard-user-to-azure.yml` with a verified domain from the target tenant. See [Configure the expected Entra tenant domain](https://github.com/{{ORG_NAME}}/{{REPOSITORY_NAME}}/blob/main/docs/configuration.md#configure-the-expected-entra-tenant-domain).
- [ ] Validate the issue forms and workflows before accepting onboarding requests.
