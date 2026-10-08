# Agent instructions

## Project purpose

This repository is a reusable GitHub template for onboarding users to an Azure tenant/subscription and a GitHub organization. A generated repository runs a one-time initializer, then uses issue forms, GitHub Actions, and file-based C# scripts to process onboarding requests.

Keep repository-specific organization, tenant, subscription, and credential values out of committed source. Use the documented placeholders or setup variables instead.

## Repository map

- [README.md](./README.md) is the entry point and contains the local validation example.
- [docs/configuration.md](./docs/configuration.md) is the detailed configuration guide; its five setup sections follow the bootstrap issue order.
- `.github/bootstrap-issues/01-...` through `05-...` are the ordered setup tasks created by the initializer.
- `.github/ISSUE_TEMPLATE/` contains the English and Korean Azure and GitHub request forms.
- `.github/workflows/init.yml` personalizes a generated repository, ensures workflow labels, creates the bootstrap issues, and removes itself and the bootstrap source files after success.
- `.github/workflows/onboard-user-to-azure.yml` and `onboard-user-to-github.yml` validate and process requests.
- `scripts/` contains standalone C# validator, onboarding, and setup scripts.

## Change coordination

- Keep each English issue form aligned with its Korean counterpart. Preserve matching field IDs, required status, request types, and labels.
- Keep the issue forms, `Validate-OnboardingRequest.cs`, and their workflows consistent. The validator accepts localized form headings and request types.
- The forms use `onboarding` and `request`; the workflows use `invalid` and `complete`. If a form or workflow adds or renames a label, update the label definitions in `init.yml`.
- Bootstrap issue numbering, file prefixes, creation order, and the corresponding sections in `docs/configuration.md` must stay aligned. Preserve the section anchors linked from the bootstrap issues.
- The initializer replaces `{{ORG_NAME}}` and `{{REPOSITORY_NAME}}` in the README, docs, issue forms, workflows other than `init.yml`, scripts, and bootstrap issue bodies. Keep `init.yml` excluded from its own replacement pass.
- Create the default-branch ruleset manually in Bootstrap issue 1 with an administrator-authenticated `gh api` call. Keep it out of `init.yml`; the initializer's `GITHUB_TOKEN` cannot administer repository settings.
- `scripts/Configure-DefaultBranchRuleset.cs` owns default-branch ruleset creation. Keep its behavior and the invocation guidance in Bootstrap issue 1 and `docs/configuration.md` aligned.
- `{{ENTRA_TENANT_DOMAIN_NAME}}` is intentionally not replaced by initialization. Configure it with `scripts/Configure-EntraTenantDomain.cs` as described in Bootstrap issue 1.
- `ONBOARDING_DUE_DATE` is optional. An unset value means ongoing onboarding; a configured non-empty value must be a valid ISO 8601 timestamp with an explicit offset.
- Values such as `--app-name`, `--team-name`, and `--group-name` in setup examples are suggestions, not required constants. The setup scripts store generated IDs in repository variables; workflows use those IDs.
- The email-domain allowlist is maintained in `Validate-OnboardingRequest.cs` and matches domains exactly. Change it only to implement an explicit onboarding policy.
- Use onboarding terminology. Preserve terms such as Microsoft Graph `invitations` only where they are required API names.
- Prefer the existing GitHub CLI (`gh`) patterns for issue and label operations; do not reintroduce deprecated issue-helper actions.

## Development and validation

- Use the .NET 10 SDK selected by [global.json](./global.json). C# scripts are file-based; run them with `dotnet run --file ./scripts/<script>.cs -- ...`. There is currently no solution, project, or .NET test project.
- For validator changes, use synthetic issue payloads and cover valid and invalid requests, both request types/localized forms when relevant, and deadlines both set and unset. Follow the example in the README's **Local validation** section.
- For issue-form or workflow changes, validate the YAML and check that both language variants and related workflow behavior remain consistent. Do not add validation dependencies solely for a one-off check.
- Run `git diff --check` before committing. Report the exact validation commands and outcomes; do not claim tests that were not run.
- Setup scripts can create or modify real GitHub and Azure resources, teams, groups, variables, and secrets. Do not run them against real accounts or subscriptions unless explicitly requested; use mocks or controlled test resources for validation.

## Security

- Never commit credentials, private keys, access tokens, or real tenant/subscription secrets. Keep `APP_PRIVATE_KEY` in GitHub Secrets; Azure onboarding uses OIDC.
- Preserve least-privilege workflow permissions and the existing environment-variable handoff for issue content. Treat issue bodies as untrusted input; do not interpolate them into shell source.
- Review changes to setup scripts and workflows for external side effects before running them.

## Documentation

- Keep the README concise and user-oriented; put configuration procedures and permissions in `docs/configuration.md`.
- When setup order, placeholders, labels, deadlines, scripts, or workflow behavior changes, update the corresponding guide and bootstrap issue instructions.
- Do not replace template values in documentation with a particular organization's values unless the initializer is intended to do so.

## Git commits and pull requests

- Make an atomic commit for each completed logical task or independent batch—not for every intermediate edit. Use Conventional Commit subjects such as `feat:`, `fix:`, `docs:`, `test:`, or `chore:`.
- After relevant checks pass, commit and push the completed task to the current branch unless the user says otherwise. Stage only changes belonging to that task; preserve unrelated pre-existing work. Do not amend or force-push unless explicitly requested.
- When asked to create or update a pull request, use [the repository PR template](./.github/PULL_REQUEST_TEMPLATE.md). Preserve its headings and checklist, replace placeholders with accurate details, and include validation commands with their results.
- If the branch already has a pull request, push updates to that branch rather than creating a duplicate.

## Before finishing

- Check the working tree and ensure no unrelated changes were staged.
- Summarize the implementation and validation performed, including any checks that could not be run.
