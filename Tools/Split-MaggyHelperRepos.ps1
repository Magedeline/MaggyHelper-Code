[CmdletBinding()]
param(
    [string]$DestinationRoot = "",
    [string]$CodeRepoName = "MaggyHelper-Code",
    [string]$MapsRepoName = "MaggyHelper-Maps",
    [string]$WorkspaceName = "MaggyHelper-Split.code-workspace",
    [switch]$InitGit,
    [switch]$Overwrite
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($DestinationRoot)) {
    $DestinationRoot = Split-Path $repoRoot -Parent
}

$DestinationRoot = [System.IO.Path]::GetFullPath($DestinationRoot)
$codeRepoPath = Join-Path $DestinationRoot $CodeRepoName
$mapsRepoPath = Join-Path $DestinationRoot $MapsRepoName
$workspacePath = Join-Path $DestinationRoot $WorkspaceName

$codeItems = @(
    ".github",
    "Code",
    "Dialog",
    "Docs",
    "Graphics",
    "Source",
    "Tools",
    "Tutorials",
    "DecalRegistry.xml",
    "everest.yaml",
    "MaggyHelper.sln",
    "MaggyHelper.snk",
    "MaggyHelperModule.cs",
    "MaggyHelperSaveData.cs",
    "MaggyHelperSession.cs",
    "MaggyHelperSettings.cs",
    "README.md",
    "SAVEDATA_UPDATES.md",
    "SkinModHelperConfig.yaml",
    "TEAM_RULESET.txt"
)

$mapItems = @(
    "Loenn",
    "MapleStarters",
    "Maps",
    "Mountain",
    "loenn-mcp"
)

$excludeDirs = @(".git", ".vs", ".venv", "bin", "obj", "__pycache__")

function Reset-Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (Test-Path $Path) {
        if (-not $Overwrite) {
            throw "Destination already exists: $Path`nUse -Overwrite to recreate it."
        }

        Remove-Item -Path $Path -Recurse -Force
    }

    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function Copy-RepoEntry {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath,
        [Parameter(Mandatory = $true)]
        [string]$TargetRoot
    )

    $sourcePath = Join-Path $repoRoot $RelativePath
    if (-not (Test-Path $sourcePath)) {
        return
    }

    $destPath = Join-Path $TargetRoot $RelativePath
    $destParent = Split-Path $destPath -Parent
    if (-not (Test-Path $destParent)) {
        New-Item -ItemType Directory -Path $destParent -Force | Out-Null
    }

    if (Test-Path $sourcePath -PathType Container) {
        New-Item -ItemType Directory -Path $destPath -Force | Out-Null

        $robocopyArgs = @(
            $sourcePath,
            $destPath,
            "/E",
            "/R:1",
            "/W:1",
            "/NFL",
            "/NDL",
            "/NJH",
            "/NJS",
            "/NP"
        )

        if ($excludeDirs.Count -gt 0) {
            $robocopyArgs += "/XD"
            $robocopyArgs += $excludeDirs
        }

        & robocopy @robocopyArgs | Out-Null
        if ($LASTEXITCODE -gt 7) {
            throw "robocopy failed for $RelativePath with exit code $LASTEXITCODE"
        }
    }
    else {
        Copy-Item -Path $sourcePath -Destination $destPath -Force
    }
}

function Write-Utf8File {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Content
    )

    $parent = Split-Path $Path -Parent
    if (-not (Test-Path $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }

    [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($false))
}

function Write-CodeRepoFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TargetRoot
    )

    $gitignore = @'
.vs/
.venv/
bin/
obj/
__pycache__/

Maps/
Mountain/
Loenn/
MapleStarters/
loenn-mcp/

.vscode/mcp.json
'@

    $tasksJson = @'
{
  "version": "2.0.0",
  "tasks": [
    {
      "type": "shell",
      "label": "dotnet build",
      "command": "dotnet",
      "args": [
        "build",
        "${workspaceFolder}/Source/MaggyHelper.csproj"
      ],
      "group": {
        "kind": "build",
        "isDefault": true
      },
      "problemMatcher": "$msCompile",
      "presentation": {
        "reveal": "silent"
      }
    }
  ]
}
'@

    $notes = @'
# Split Repo Notes

This repository is the runtime side of the MaggyHelper split.

Use it for:

- `Source/` changes
- `Graphics/` and runtime content changes
- builds and release packaging

When you need map-side content locally, run the sync script from the sibling map repository:

`Tools/Sync-ToCodeRepo.ps1`
'@

    Write-Utf8File -Path (Join-Path $TargetRoot ".gitignore") -Content $gitignore
    Write-Utf8File -Path (Join-Path $TargetRoot ".vscode\tasks.json") -Content $tasksJson
    Write-Utf8File -Path (Join-Path $TargetRoot "SPLIT_REPO_NOTES.md") -Content $notes
}

function Write-MapRepoFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TargetRoot,
        [Parameter(Mandatory = $true)]
        [string]$DefaultCodeRepoName
    )

    $gitignore = @'
.vs/
.venv/
bin/
obj/
__pycache__/

Code/
Source/
Graphics/
Dialog/

Maps/Maggy/Temp/
.vscode/mcp.json
'@

    $tasksJson = @'
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "Preview .bin Map",
      "detail": "Open the active .bin file as an interactive HTML preview in the browser",
      "type": "shell",
      "command": "${workspaceFolder}\\.venv\\Scripts\\python.exe",
      "args": [
        "${workspaceFolder}\\loenn-mcp\\preview_map.py",
        "${file}"
      ],
      "group": "build",
      "presentation": {
        "reveal": "always",
        "panel": "shared",
        "clear": true
      },
      "problemMatcher": []
    },
    {
      "label": "Open Map .bin...",
      "detail": "Pick how to open the active .bin file - Loenn MCP preview or raw binary",
      "type": "shell",
      "command": "if ('${input:binOpenMode}' -eq 'loenn') { & '${workspaceFolder}\\.venv\\Scripts\\python.exe' '${workspaceFolder}\\loenn-mcp\\preview_map.py' '${file}' } else { code --reuse-window '${file}' }",
      "options": {
        "shell": {
          "executable": "powershell.exe",
          "args": ["-NoProfile", "-Command"]
        }
      },
      "group": "build",
      "presentation": {
        "reveal": "always",
        "panel": "shared",
        "clear": true
      },
      "problemMatcher": []
    },
    {
      "label": "Maple: Generate + Preview Starter",
      "detail": "Run a Maple starter script, then open Loenn MCP HTML preview for its output map",
      "type": "shell",
      "command": "& '${workspaceFolder}\\MapleStarters\\run_maple_and_preview.ps1' -Starter '${input:mapleStarterScript}'",
      "options": {
        "shell": {
          "executable": "powershell.exe",
          "args": ["-NoProfile", "-ExecutionPolicy", "Bypass", "-Command"]
        }
      },
      "group": "build",
      "presentation": {
        "reveal": "always",
        "panel": "shared",
        "clear": true
      },
      "problemMatcher": []
    },
    {
      "label": "Loenn MCP: Generate + Preview Starter",
      "detail": "Generate a starter .bin map with Loenn MCP and open interactive HTML preview",
      "type": "shell",
      "command": "${workspaceFolder}\\.venv\\Scripts\\python.exe",
      "args": [
        "${workspaceFolder}\\loenn-mcp\\generate_starter_and_preview.py",
        "${workspaceFolder}\\Maps\\Maggy\\${input:loennStarterOutput}.bin",
        "${input:loennStarterTemplate}",
        "Maggy/${input:loennStarterOutput}",
        "${input:loennStarterRoomPrefix}"
      ],
      "group": "build",
      "presentation": {
        "reveal": "always",
        "panel": "shared",
        "clear": true
      },
      "problemMatcher": []
    },
    {
      "label": "Loenn MCP: Generate Custom + Preview",
      "detail": "Generate a .bin map from custom room JSON and open interactive HTML preview",
      "type": "shell",
      "command": "${workspaceFolder}\\.venv\\Scripts\\python.exe",
      "args": [
        "${workspaceFolder}\\loenn-mcp\\generate_custom_starter_and_preview.py",
        "${workspaceFolder}\\Maps\\Maggy\\${input:loennCustomOutput}.bin",
        "${input:loennCustomRoomsFile}",
        "Maggy/${input:loennCustomOutput}",
        "${input:loennCustomRoomPrefix}"
      ],
      "group": "build",
      "presentation": {
        "reveal": "always",
        "panel": "shared",
        "clear": true
      },
      "problemMatcher": []
    },
    {
      "label": "Loenn MCP: Generate Weighted + Preview",
      "detail": "Generate a weighted room sequence from a pool JSON and open interactive HTML preview",
      "type": "shell",
      "command": "${workspaceFolder}\\.venv\\Scripts\\python.exe",
      "args": [
        "${workspaceFolder}\\loenn-mcp\\generate_weighted_and_preview.py",
        "${workspaceFolder}\\Maps\\Maggy\\${input:loennWeightedOutput}.bin",
        "${input:loennWeightedPoolFile}",
        "${input:loennWeightedRoomCount}",
        "${input:loennWeightedSeed}",
        "Maggy/${input:loennWeightedOutput}",
        "${input:loennWeightedRoomPrefix}",
        "--required-tags",
        "${input:loennWeightedRequiredTags}",
        "--min-tag-counts",
        "${input:loennWeightedMinTagCounts}",
        "--excluded-adjacent-tag-pairs",
        "${input:loennWeightedExcludedAdjacentTagPairs}",
        "--late-game-tags",
        "${input:loennWeightedLateGameTags}",
        "--late-game-start-ratio",
        "${input:loennWeightedLateGameStartRatio}",
        "--report-out",
        "${input:loennWeightedReportPath}"
      ],
      "group": "build",
      "presentation": {
        "reveal": "always",
        "panel": "shared",
        "clear": true
      },
      "problemMatcher": []
    }
  ],
  "inputs": [
    {
      "id": "binOpenMode",
      "type": "pickString",
      "description": "Open .bin map file with:",
      "options": [
        {
          "label": "Loenn MCP - interactive HTML preview in browser",
          "value": "loenn"
        },
        {
          "label": "Binary - open raw file in VS Code editor",
          "value": "binary"
        }
      ],
      "default": "loenn"
    },
    {
      "id": "mapleStarterScript",
      "type": "pickString",
      "description": "Choose Maple starter script to generate and preview:",
      "options": [
        "starter_minimal_map.jl",
        "starter_entity_map.jl",
        "starter_route_map.jl"
      ],
      "default": "starter_route_map.jl"
    },
    {
      "id": "loennStarterTemplate",
      "type": "pickString",
      "description": "Choose starter template:",
      "options": [
        "minimal",
        "entity",
        "route"
      ],
      "default": "route"
    },
    {
      "id": "loennStarterOutput",
      "type": "promptString",
      "description": "Output map filename (without .bin):",
      "default": "LoennMcpStarter"
    },
    {
      "id": "loennStarterRoomPrefix",
      "type": "promptString",
      "description": "Optional room prefix:",
      "default": ""
    },
    {
      "id": "loennCustomRoomsFile",
      "type": "promptString",
      "description": "Path to custom room JSON file:",
      "default": "${workspaceFolder}\\loenn-mcp\\custom_rooms_example.json"
    },
    {
      "id": "loennCustomOutput",
      "type": "promptString",
      "description": "Output custom map filename (without .bin):",
      "default": "LoennMcpCustomStarter"
    },
    {
      "id": "loennCustomRoomPrefix",
      "type": "promptString",
      "description": "Optional custom room prefix:",
      "default": ""
    },
    {
      "id": "loennWeightedPoolFile",
      "type": "pickString",
      "description": "Choose weighted room pool preset:",
      "options": [
        "${workspaceFolder}\\loenn-mcp\\pcg_room_pool.example.json",
        "${workspaceFolder}\\loenn-mcp\\pcg_room_pool.movement_focus.json"
      ],
      "default": "${workspaceFolder}\\loenn-mcp\\pcg_room_pool.example.json"
    },
    {
      "id": "loennWeightedRoomCount",
      "type": "promptString",
      "description": "Number of rooms to generate:",
      "default": "7"
    },
    {
      "id": "loennWeightedSeed",
      "type": "promptString",
      "description": "Optional random seed (blank = random):",
      "default": ""
    },
    {
      "id": "loennWeightedOutput",
      "type": "promptString",
      "description": "Output weighted map filename (without .bin):",
      "default": "LoennMcpWeightedStarter"
    },
    {
      "id": "loennWeightedRoomPrefix",
      "type": "promptString",
      "description": "Optional weighted room prefix:",
      "default": ""
    },
    {
      "id": "loennWeightedRequiredTags",
      "type": "promptString",
      "description": "Optional override: required tags as comma-separated values:",
      "default": ""
    },
    {
      "id": "loennWeightedMinTagCounts",
      "type": "promptString",
      "description": "Optional override: min tag counts as tag=count pairs or JSON:",
      "default": ""
    },
    {
      "id": "loennWeightedExcludedAdjacentTagPairs",
      "type": "promptString",
      "description": "Optional override: excluded adjacent tag pairs as tag>tag pairs or JSON:",
      "default": ""
    },
    {
      "id": "loennWeightedLateGameTags",
      "type": "promptString",
      "description": "Optional override: late-game tags as comma-separated values:",
      "default": ""
    },
    {
      "id": "loennWeightedLateGameStartRatio",
      "type": "promptString",
      "description": "Optional override: late-game start ratio (0.0-1.0):",
      "default": ""
    },
    {
      "id": "loennWeightedReportPath",
      "type": "promptString",
      "description": "Optional report output path (blank disables report):",
      "default": "${workspaceFolder}\\Maps\\Maggy\\Temp\\weighted_sequence_report.json"
    }
  ]
}
'@

    $syncScript = @'
[CmdletBinding()]
param(
    [string]$CodeRepoPath = "",
    [switch]$Mirror
)

$ErrorActionPreference = "Stop"

$mapsRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($CodeRepoPath)) {
    $CodeRepoPath = Join-Path (Split-Path $mapsRoot -Parent) "__CODE_REPO_NAME__"
}

$CodeRepoPath = [System.IO.Path]::GetFullPath($CodeRepoPath)
if (-not (Test-Path $CodeRepoPath)) {
    throw "Code repo not found: $CodeRepoPath"
}

$items = @("Loenn", "MapleStarters", "Maps", "Mountain", "loenn-mcp")
$excludeDirs = @(".git", ".vs", ".venv", "bin", "obj", "__pycache__")

foreach ($item in $items) {
    $sourcePath = Join-Path $mapsRoot $item
    if (-not (Test-Path $sourcePath)) {
        continue
    }

    $destPath = Join-Path $CodeRepoPath $item
    New-Item -ItemType Directory -Path $destPath -Force | Out-Null

    $args = @(
        $sourcePath,
        $destPath,
        "/E",
        "/R:1",
        "/W:1",
        "/NFL",
        "/NDL",
        "/NJH",
        "/NJS",
        "/NP"
    )

    if ($Mirror) {
        $args += "/MIR"
    }

    if ($excludeDirs.Count -gt 0) {
        $args += "/XD"
        $args += $excludeDirs
    }

    & robocopy @args | Out-Null
    if ($LASTEXITCODE -gt 7) {
        throw "robocopy failed for $item with exit code $LASTEXITCODE"
    }
}

Write-Host "Map-side content synced into: $CodeRepoPath"
if (-not $Mirror) {
    Write-Host "Deletes were not mirrored. Re-run with -Mirror if you want exact directory mirroring."
}
'@

    $syncScript = $syncScript.Replace("__CODE_REPO_NAME__", $DefaultCodeRepoName)

    $notes = @'
# Split Repo Notes

This repository is the map-authoring side of the MaggyHelper split.

Use it for:

- `Maps/`
- `Loenn/`
- `MapleStarters/`
- `loenn-mcp/`

When you need the full playable mod assembled in the sibling code repository, run:

`Tools/Sync-ToCodeRepo.ps1`
'@

    Write-Utf8File -Path (Join-Path $TargetRoot ".gitignore") -Content $gitignore
    Write-Utf8File -Path (Join-Path $TargetRoot ".vscode\tasks.json") -Content $tasksJson
    Write-Utf8File -Path (Join-Path $TargetRoot "Tools\Sync-ToCodeRepo.ps1") -Content $syncScript
    Write-Utf8File -Path (Join-Path $TargetRoot "SPLIT_REPO_NOTES.md") -Content $notes
}

function Write-WorkspaceFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$CodeFolder,
        [Parameter(Mandatory = $true)]
        [string]$MapsFolder
    )

    $content = @"
{
  "folders": [
    { "path": "./$CodeFolder" },
    { "path": "./$MapsFolder" }
  ],
  "settings": {
    "files.exclude": {
      "**/bin": true,
      "**/obj": true,
      "**/__pycache__": true
    }
  }
}
"@

    Write-Utf8File -Path $Path -Content $content
}

Write-Host "Creating split repositories under: $DestinationRoot"

Reset-Directory -Path $codeRepoPath
Reset-Directory -Path $mapsRepoPath

foreach ($item in $codeItems) {
    Copy-RepoEntry -RelativePath $item -TargetRoot $codeRepoPath
}

foreach ($item in $mapItems) {
    Copy-RepoEntry -RelativePath $item -TargetRoot $mapsRepoPath
}

Write-CodeRepoFiles -TargetRoot $codeRepoPath
Write-MapRepoFiles -TargetRoot $mapsRepoPath -DefaultCodeRepoName $CodeRepoName
Write-WorkspaceFile -Path $workspacePath -CodeFolder $CodeRepoName -MapsFolder $MapsRepoName

if ($InitGit) {
    Push-Location $codeRepoPath
    try {
        git init | Out-Null
    }
    finally {
        Pop-Location
    }

    Push-Location $mapsRepoPath
    try {
        git init | Out-Null
    }
    finally {
        Pop-Location
    }
}

Write-Host "Created: $codeRepoPath"
Write-Host "Created: $mapsRepoPath"
Write-Host "Workspace: $workspacePath"
Write-Host "Next: open the workspace file, then use the map repo sync script when you need a full assembled mod in the code repo."