# [Bootstrap 4/5] Set up the Azure service principal

Configure the Azure workload identity used by the Azure onboarding workflow. Complete [Bootstrap 3/5](https://github.com/{{ORG_NAME}}/{{REPOSITORY_NAME}}/issues) first.

## Steps

- [ ] Review [Create the Azure workload identity](https://github.com/{{ORG_NAME}}/{{REPOSITORY_NAME}}/blob/main/docs/configuration.md#create-the-azure-workload-identity), including the permissions and subscription scope it grants.
- [ ] Install the .NET 10 SDK, Azure CLI, and GitHub CLI; sign in to the intended Azure tenant and subscription, and authenticate `gh` for this repository.
- [ ] Choose a display name for the Azure app registration and replace the example `onboarding-automation` value for `--app-name` in the command below.
- [ ] Run the setup script:

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

- [ ] Review the generated permissions. The script currently assigns `Contributor` at subscription scope.
- [ ] Confirm `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID` are set as repository variables.
- [ ] Confirm the GitHub Actions federated credential trusts the repository's `main` branch.
