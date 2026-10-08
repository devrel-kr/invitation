# [Bootstrap 2/4] Set up the GitHub onboarding team

Create or reuse the team that receives users after GitHub organization onboarding. Complete [Bootstrap 1/4](https://github.com/{{ORG_NAME}}/{{REPOSITORY_NAME}}/issues) first so the GitHub App is ready for the onboarding workflow.

## Steps

- [ ] Review [Create the GitHub onboarding team](https://github.com/{{ORG_NAME}}/{{REPOSITORY_NAME}}/blob/main/docs/configuration.md#create-the-github-onboarding-team).
- [ ] Authenticate `gh` as an organization member who can manage teams and repository Actions variables.
- [ ] Choose the team name for this organization and replace the example `onboarding-participants` value for `--team-name` in the command below.
- [ ] Run the setup script:

  ```bash
  # zsh/bash
  dotnet run --file ./scripts/Setup-GitHubTeam.cs -- \
    --github-org "{{ORG_NAME}}" \
    --team-name "onboarding-participants" \
    --description "Users onboarded by this workflow." \
    --privacy "closed" \
    --github-repo "{{ORG_NAME}}/{{REPOSITORY_NAME}}"
  ```

  ```powershell
  # PowerShell
  dotnet run --file ./scripts/Setup-GitHubTeam.cs -- `
    --github-org "{{ORG_NAME}}" `
    --team-name "onboarding-participants" `
    --description "Users onboarded by this workflow." `
    --privacy "closed" `
    --github-repo "{{ORG_NAME}}/{{REPOSITORY_NAME}}"
  ```

- [ ] Confirm `GITHUB_TEAM_ID` is set as a repository variable and identifies the intended team.
- [ ] Confirm the GitHub App from Bootstrap 1/4 is installed on the organization.
