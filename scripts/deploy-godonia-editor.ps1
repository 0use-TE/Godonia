# Deploy Godonia runtime next to a custom Godot editor.
# Does not bump Ouse.Godonia's NuGet version. Keeps 1.0.0 and overwrites the local nupkg.
# Usage:
#   .\scripts\deploy-godonia-editor.ps1
#   .\scripts\deploy-godonia-editor.ps1 -GodotBin C:\path\to\godot\bin
#
# When copying the editor to another PC, copy the entire bin folder, including:
#   GodotSharp\Godonia\project-template   (Avalonia starter: UI + Editor.Preview)
#   GodotSharp\Godonia\nupkg              (Ouse.Godonia.1.0.0.nupkg)
#   GodotSharp\Godonia\runtime
# Copying only the .exe yields a vanilla Godot C# project with no Avalonia.

param(
	[string]$GodotBin = ""
)

$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
if (-not $GodotBin) {
	if ($env:GODOT_BIN) {
		$GodotBin = $env:GODOT_BIN
	}
	elseif (Test-Path (Join-Path $repo "godot\bin")) {
		$GodotBin = Join-Path $repo "godot\bin"
	}
	else {
		$GodotBin = "C:\Users\Ouse\Desktop\Projects\godot\bin"
	}
}

$destRoot = Join-Path $GodotBin "GodotSharp\Godonia"
$runtimeDir = Join-Path $destRoot "runtime"
$nupkgDir = Join-Path $destRoot "nupkg"
$templateDir = Join-Path $destRoot "project-template"
$templateSrc = Join-Path $repo "editor\project-template"
$lib = Join-Path $repo "src\Ouse.Godonia\Ouse.Godonia.csproj"

if (-not (Test-Path $GodotBin)) {
	throw "Godot bin folder not found: $GodotBin"
}

Write-Host "Publishing Godonia runtime -> $runtimeDir"
New-Item -ItemType Directory -Force -Path $runtimeDir, $nupkgDir | Out-Null
dotnet publish $lib -c Debug -r win-x64 --no-self-contained -p:CopyLocalLockFileAssemblies=true -o $runtimeDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Write-Host "Packing Ouse.Godonia -> $nupkgDir"
dotnet pack $lib -c Debug -o $nupkgDir --no-restore
if ($LASTEXITCODE -ne 0) {
	dotnet pack $lib -c Debug -o $nupkgDir
	if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed" }
}

# Same Version (1.0.0) is overwritten in-place; NuGet global cache will NOT refresh unless cleared.
$globalCache = Join-Path $env:USERPROFILE ".nuget\packages\ouse.godonia\1.0.0"
if (Test-Path $globalCache) {
	Write-Host "Clearing stale NuGet cache $globalCache"
	Remove-Item $globalCache -Recurse -Force
}

$packagesDir = Join-Path $destRoot "packages"
if (Test-Path $packagesDir) {
	Write-Host "Removing leftover editor-local package cache $packagesDir"
	Remove-Item $packagesDir -Recurse -Force
}

if (Test-Path $templateSrc) {
	Write-Host "Syncing project-template -> $templateDir"
	if (Test-Path $templateDir) {
		Remove-Item $templateDir -Recurse -Force
	}
	New-Item -ItemType Directory -Force -Path $templateDir | Out-Null
	Get-ChildItem $templateSrc -Force | Where-Object { $_.Name -notin @('bin', 'obj', '.godot') } | ForEach-Object {
		Copy-Item $_.FullName (Join-Path $templateDir $_.Name) -Recurse -Force
	}
	$toolsTemplate = Join-Path $GodotBin "GodotSharp\Tools\project-template"
	if (Test-Path $toolsTemplate) {
		Remove-Item $toolsTemplate -Recurse -Force
	}
	Copy-Item $templateDir $toolsTemplate -Recurse -Force
}
else {
	Write-Warning "No editor\project-template in repo; Godonia new-project bootstrap will be skipped."
}

Write-Host "Done. Restart the custom Godot editor."
Write-Host "  Runtime: $runtimeDir"
Write-Host "  NuGet:   $nupkgDir"
Write-Host "  Template: $templateDir"
Write-Host "Rebuild GodotPlugins.dll (python modules/mono/build_scripts/build_assemblies.py --godot-output-dir bin) if you changed Godot C#."
