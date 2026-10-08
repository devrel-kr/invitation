# [Bootstrap 4/4] Set up the Microsoft Entra security group

Create or reuse the Microsoft Entra security group that receives Azure-onboarded users, then finish the remaining Azure configuration. Complete [Bootstrap 3/4](https://github.com/{{ORG_NAME}}/{{REPOSITORY_NAME}}/issues) first so the Azure workload identity is ready.

## Steps

- [ ] Review [Create the Azure security group](https://github.com/{{ORG_NAME}}/{{REPOSITORY_NAME}}/blob/main/docs/configuration.md#create-the-azure-security-group).
- [ ] Sign in to the intended Azure tenant and authenticate `gh` for this repository.
- [ ] Choose the intended security group display name; replace the example `onboarding-participants` value for `--group-name` in the command below if needed.
- [ ] Run the setup script:

  ```bash
  # zsh/bash
  dotnet run --file ./scripts/Setup-EntraSecurityGroup.cs -- \
    --group-name "onboarding-participants" \
    --description "Users onboarded by this workflow." \
    --github-repo "{{ORG_NAME}}/{{REPOSITORY_NAME}}"
  ```

  ```powershell
  # PowerShell
  dotnet run --file ./scripts/Setup-EntraSecurityGroup.cs -- `
    --group-name "onboarding-participants" `
    --description "Users onboarded by this workflow." `
    --github-repo "{{ORG_NAME}}/{{REPOSITORY_NAME}}"
  ```

- [ ] Confirm `AZURE_SECURITY_GROUP_ID` is set as a repository variable and identifies the intended security-enabled group.
- [ ] If onboarding should end on a deadline, set `ONBOARDING_DUE_DATE` as a repository variable using an ISO 8601 timestamp with an explicit UTC offset; leave it unset for ongoing onboarding.
- [ ] Review the repository variables and secret listed in the [configuration overview](https://github.com/{{ORG_NAME}}/{{REPOSITORY_NAME}}/blob/main/docs/configuration.md#configuration-overview), then validate the issue forms and workflows before accepting requests.
