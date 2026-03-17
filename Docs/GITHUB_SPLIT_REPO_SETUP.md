# GitHub Split Repo Setup

This guide assumes you used `Tools/Split-MaggyHelperRepos.ps1` and created these two local sibling folders:

- `MaggyHelper-Code`
- `MaggyHelper-Maps`

It is tailored to the current GitHub owner and source repo:

- owner: `Magedeline`
- source repo: `DesoloZantas`

## Target GitHub Repos

Create these two new repositories under the `Magedeline` account:

- `MaggyHelper-Code`
- `MaggyHelper-Maps`

Recommended URLs:

- `https://github.com/new?owner=Magedeline&name=MaggyHelper-Code`
- `https://github.com/new?owner=Magedeline&name=MaggyHelper-Maps`

## Local Split First

From the current monorepo root:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Split-MaggyHelperRepos.ps1 -InitGit
```

That creates sibling folders next to this repo.

## Push The Code Repo

Run these commands from the parent folder that now contains `MaggyHelper-Code` and `MaggyHelper-Maps`:

```powershell
Set-Location ..\MaggyHelper-Code
git branch -M main
git add .
git commit -m "Initial split from DesoloZantas"
git remote add origin https://github.com/Magedeline/MaggyHelper-Code.git
git push -u origin main
```

## Push The Map Repo

```powershell
Set-Location ..\MaggyHelper-Maps
git branch -M main
git add .
git commit -m "Initial split from DesoloZantas"
git remote add origin https://github.com/Magedeline/MaggyHelper-Maps.git
git push -u origin main
```

## Optional: Keep The Source Repo As Archive

If `DesoloZantas` is no longer the active working repo, one practical approach is:

1. leave it read-only as the historical monorepo
2. update its README to point contributors to the two new repos
3. stop committing new runtime or map work there

## Optional: Make The Code Repo Pull In Map Content

When you need a full assembled mod tree inside `MaggyHelper-Code`, run this from the map repo:

```powershell
Set-Location ..\MaggyHelper-Maps
powershell -ExecutionPolicy Bypass -File .\Tools\Sync-ToCodeRepo.ps1
```

If you want exact mirroring, including deletes:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Sync-ToCodeRepo.ps1 -Mirror
```

## If A Remote Already Exists

If you re-run setup and `origin` already exists, replace the remote instead of adding it again:

```powershell
git remote set-url origin https://github.com/Magedeline/MaggyHelper-Code.git
```

or:

```powershell
git remote set-url origin https://github.com/Magedeline/MaggyHelper-Maps.git
```

## Suggested Visibility

- `MaggyHelper-Code`: private if it contains unreleased runtime work
- `MaggyHelper-Maps`: private if you want to avoid publishing chapter files and generated previews early

## Suggested First Cleanup After Push

1. enable Git LFS if you want `.bin` maps and large assets tracked efficiently
2. add branch protection on `main`
3. add a short README in each new repo describing its responsibility
4. update the old monorepo README with links to the split repos

## Notes About GitHub CLI

This machine does not currently have `gh` installed, so the commands above use plain `git` plus the GitHub web UI for repo creation.
