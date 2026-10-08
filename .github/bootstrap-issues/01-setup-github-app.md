# [Bootstrap 1/4] Set up the GitHub App

Configure the GitHub App used by the onboarding workflows. Complete this issue before setting up the onboarding team.

## Steps

- [ ] Review [Create the GitHub App](https://github.com/{{ORG_NAME}}/{{REPOSITORY_NAME}}/blob/main/docs/configuration.md#create-the-github-app).
- [ ] Install the .NET 10 SDK and GitHub CLI, then authenticate `gh` as an organization owner.
- [ ] Run the setup script:

  ```bash
  dotnet run --file ./scripts/Setup-GitHubApp.cs -- \
    --app-name "onboarding-automation" \
    --github-org "{{ORG_NAME}}" \
    --github-repo "{{ORG_NAME}}/{{REPOSITORY_NAME}}"
  ```

- [ ] Install the created app on the organization and grant it access to this repository.
- [ ] Confirm that `APP_CLIENT_ID` is a repository variable and `APP_PRIVATE_KEY` is a repository secret.
- [ ] Confirm the app has repository `Contents: read` and `Issues: write`, and organization `Members: write` permissions.
