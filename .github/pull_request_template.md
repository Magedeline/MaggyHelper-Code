# Pull Request

## Summary

Describe the change and the user-facing or maintainer-facing impact.

## Scope

- [ ] Code
- [ ] Maps or content
- [ ] Loenn tooling
- [ ] Docs
- [ ] Build or CI
- [ ] Security-sensitive change

## Validation

- [ ] `dotnet build Source/MaggyHelper.csproj` passed locally or in CI
- [ ] Affected gameplay/content was validated
- [ ] Docs updated if behavior or workflow changed

## Security Review

- [ ] No credentials, tokens, API keys, private URLs, or local config were added
- [ ] Any new dependency was reviewed for necessity and source trust
- [ ] Any file handling, path handling, network usage, or serialization change was reviewed for abuse cases
- [ ] If secrets were used locally, they remain in untracked files or GitHub repository secrets only

## Risk Notes

List risky areas, rollback notes, or follow-up work.

## Review Routing

Tag the most relevant reviewer for the affected area and note any domain-specific checks reviewers should perform.

