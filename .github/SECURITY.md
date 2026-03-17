# Security Policy

## Supported Branches

Security fixes are applied to the default branch and then backported only when needed.

| Branch | Supported |
| --- | --- |
| main | Yes |
| master | Yes |
| other branches | Best effort |

## Reporting A Vulnerability

Do not open public issues for suspected vulnerabilities or leaked credentials.

Report security concerns privately to the maintainers with:

- a clear description of the issue
- exact file paths or features involved
- reproduction steps if applicable
- whether credentials, tokens, or private assets may have been exposed

If a credential leak is suspected:

1. Revoke or rotate the credential immediately.
2. Remove it from the repository history if it was committed.
3. Open a private report to the maintainers with the affected scope.

## Repository Hardening

This repository uses GitHub-native security automation:

- CodeQL analysis for C# and Python
- dependency review on pull requests
- Dependabot updates for NuGet, pip, and GitHub Actions
- gitleaks secret scanning on pushes and pull requests

## Maintainer Guidance

- Keep the repository private until content is ready to ship.
- Enable GitHub Secret Scanning and Push Protection in repository settings if your plan supports it.
- Require pull requests and status checks before merging into protected branches.
- Store local API keys and tokens in untracked local files or repository secrets only.
