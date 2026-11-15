<#
.SYNOPSIS
  Build and publish a versioned Crypt Sage release to GitHub.

.DESCRIPTION
  Reads the version from src/CryptSage.App/CryptSage.App.csproj, validates a
  clean Git state, builds the single-file app and the Inno Setup installer,
  pushes the current commit and version tag, then creates a public GitHub
  release that contains the installer. The in-app updater
  (src/CryptSage.Core/Updater.cs) reads that release. After the new release is
  confirmed live, all older releases and their tags are deleted.

  Change <Version> in CryptSage.App.csproj and commit before this script.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-Location -Path $PSScriptRoot

function Invoke-Native {
  <#
  .SYNOPSIS
    Run a native command (git, gh, dotnet, ISCC) safely under Windows PowerShell 5.1.

  .DESCRIPTION
    Windows PowerShell 5.1 converts a native command's stderr output into
    NativeCommandError records. With $ErrorActionPreference = "Stop" the first
    such line aborts the script even though the command succeeded. This wrapper
    runs the command with the preference set to Continue, restores it
    afterwards, and fails on the exit code instead.
  #>
  param(
    [Parameter(Mandatory = $true, Position = 0)] [scriptblock] $Command,
    [string] $FailureMessage,
    [switch] $AllowFailure
  )
  $previousPreference = $ErrorActionPreference
  $ErrorActionPreference = "Continue"
  $global:LASTEXITCODE = 0
  try { & $Command }
  finally { $ErrorActionPreference = $previousPreference }
  if (-not $AllowFailure -and $LASTEXITCODE -ne 0) {
    if (-not $FailureMessage) { $FailureMessage = "Command failed with exit code ${LASTEXITCODE}: $Command" }
    throw $FailureMessage
  }
}

$repo = "rmirabelle/cryptsage"
$csproj = "src/CryptSage.App/CryptSage.App.csproj"
$match = [regex]::Match((Get-Content $csproj -Raw), '<Version>([^<]+)</Version>')
if (-not $match.Success) { throw "Could not read <Version> from $csproj" }

$version = $match.Groups[1].Value
$tag = "v$version"
$assetName = "CryptSage_${version}_x64-setup.exe"
$assetPath = "dist/$assetName"

$iscc = Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"
if (-not (Test-Path -LiteralPath $iscc -PathType Leaf)) { throw "Inno Setup 6 is missing: $iscc" }
if (-not (Test-Path -LiteralPath "installer/vendor/VeraCrypt/VeraCrypt-x64.exe" -PathType Leaf)) {
  throw "The bundled VeraCrypt files are missing: installer/vendor/VeraCrypt"
}

Invoke-Native -FailureMessage "Crypt Sage is not a Git repository." { git rev-parse --is-inside-work-tree *> $null }
$dirtyFiles = Invoke-Native -FailureMessage "Could not read the Git working tree status." { git status --porcelain }
if ($dirtyFiles) { throw "The working tree must be clean before publishing." }

$origin = Invoke-Native -AllowFailure { git remote get-url origin }
if ($LASTEXITCODE -ne 0 -or $origin -notmatch 'github\.com[/:]rmirabelle/cryptsage(?:\.git)?$') {
  throw "The origin remote must be the public $repo GitHub repository."
}

Invoke-Native -FailureMessage "GitHub CLI is not authenticated. Run: gh auth login -h github.com" { gh auth status *> $null }

Invoke-Native -AllowFailure { gh release view $tag *> $null }
if ($LASTEXITCODE -eq 0) { throw "Release $tag already exists." }

if (Get-Process -Name "CryptSage.App" -ErrorAction SilentlyContinue) {
  throw "Crypt Sage is open. Close it, then publish again."
}

Write-Host "==> Building Crypt Sage $tag ..." -ForegroundColor Cyan
Invoke-Native -FailureMessage "dotnet publish failed" {
  dotnet publish src/CryptSage.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -o publish
}

$builtVersion = (Get-Item "publish/CryptSage.App.exe").VersionInfo.ProductVersion
if (-not $builtVersion.StartsWith($version)) {
  throw "The built executable has version $builtVersion, not $version."
}

Invoke-Native -FailureMessage "Inno Setup build failed" { & $iscc "/DMyAppVersion=$version" "installer\CryptSage.iss" }

$installer = Get-Item "installer/Output/CryptSage-Setup.exe"
if ($installer.Length -le 0) { throw "The generated installer is empty: $($installer.FullName)" }
New-Item -ItemType Directory -Force -Path "dist" | Out-Null
Copy-Item $installer.FullName $assetPath -Force

Write-Host "==> Pushing source and tag $tag ..." -ForegroundColor Cyan
Invoke-Native -FailureMessage "Could not push the release commit" { git push origin HEAD }
Invoke-Native -FailureMessage "Could not create tag $tag" { git tag -a $tag -m "Crypt Sage $tag" }
Invoke-Native -FailureMessage "Could not push tag $tag" { git push origin $tag }

Write-Host "==> Publishing GitHub release $tag ..." -ForegroundColor Cyan
Invoke-Native -FailureMessage "Could not create GitHub release $tag" { gh release create $tag $assetPath --verify-tag --title "Crypt Sage $tag" --generate-notes --latest }

$latestReleaseJson = Invoke-Native -FailureMessage "Could not verify the latest-release endpoint. Older releases were preserved." { gh api "repos/$repo/releases/latest" }
$latestRelease = $latestReleaseJson | ConvertFrom-Json
$latestAssets = @($latestRelease.assets | ForEach-Object { $_.name })
if ($latestRelease.tag_name -ne $tag -or $latestRelease.draft -or $latestRelease.prerelease -or $latestAssets -notcontains $assetName) {
  throw "GitHub's releases/latest endpoint does not resolve to the newly published Crypt Sage installer. Older releases were preserved."
}

Write-Host "==> Removing superseded releases ..." -ForegroundColor Cyan
$priorReleases = @(Invoke-Native -FailureMessage "Could not list existing GitHub releases" { gh release list --limit 100 --json tagName -q '.[].tagName' })
foreach ($priorTag in $priorReleases) {
  if ($priorTag -and $priorTag -ne $tag) {
    Write-Host "    - deleting $priorTag"
    Invoke-Native -FailureMessage "Could not delete superseded release $priorTag" { gh release delete $priorTag --yes --cleanup-tag }
  }
}

Write-Host "==> Published https://github.com/$repo/releases/tag/$tag" -ForegroundColor Green
