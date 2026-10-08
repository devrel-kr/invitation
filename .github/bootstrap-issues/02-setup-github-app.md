# [Bootstrap 2/5] Set up the GitHub App

Configure the GitHub App used by the onboarding workflows. Complete [Bootstrap 1/5](https://github.com/{{ORG_NAME}}/{{REPOSITORY_NAME}}/issues) first to configure repository settings and the Entra tenant domain.

## Steps

- [ ] Review [Create the GitHub App](https://github.com/{{ORG_NAME}}/{{REPOSITORY_NAME}}/blob/main/docs/configuration.md#create-the-github-app).
- [ ] Install the .NET 10 SDK and GitHub CLI, then authenticate `gh` as an organization owner.
- [ ] Choose a name for the GitHub App and replace the example `onboarding-automation` value for `--app-name` in the command below.
- [ ] Run the setup script:

  ```bash
  # zsh/bash
  dotnet run --file ./scripts/Setup-GitHubApp.cs -- \
    --app-name "onboarding-automation" \
    --github-org "{{ORG_NAME}}" \
    --github-repo "{{ORG_NAME}}/{{REPOSITORY_NAME}}"
  ```

  ```powershell
  # PowerShell
  dotnet run --file ./scripts/Setup-GitHubApp.cs -- `
    --app-name "onboarding-automation" `
    --github-org "{{ORG_NAME}}" `
    --github-repo "{{ORG_NAME}}/{{REPOSITORY_NAME}}"
  ```

- [ ] Install the created app on the organization and grant it access to this repository.
- [ ] Confirm that `APP_CLIENT_ID` is a repository variable and `APP_PRIVATE_KEY` is a repository secret.
- [ ] Confirm the app has repository `Contents: read` and `Issues: write`, and organization `Members: write` permissions.
