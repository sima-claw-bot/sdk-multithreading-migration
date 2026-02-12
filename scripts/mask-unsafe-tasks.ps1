<#
.SYNOPSIS
    Masks unsafe task implementations for the migration evaluation pipeline.
.DESCRIPTION
    Copies each .cs file from UnsafeThreadSafeTasks/ into MaskedTasks/,
    changing the namespace to MaskedTasks and replacing Execute() method
    bodies with throw new NotImplementedException().
#>

param(
    [string]$RepoRoot = (Split-Path $PSScriptRoot -Parent),
    [string]$SourceDir = "UnsafeThreadSafeTasks",
    [string]$TargetDir = "MaskedTasks"
)

$srcPath = Join-Path $RepoRoot $SourceDir
$dstPath = Join-Path $RepoRoot $TargetDir

if (Test-Path $dstPath) {
    Remove-Item $dstPath -Recurse -Force
}
New-Item -ItemType Directory -Path $dstPath -Force | Out-Null

$csFiles = Get-ChildItem -Path $srcPath -Filter "*.cs" -Recurse

foreach ($file in $csFiles) {
    $relativePath = $file.FullName.Substring($srcPath.Length + 1)
    $destFile = Join-Path $dstPath $relativePath
    $destDir = Split-Path $destFile -Parent

    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    }

    $content = Get-Content $file.FullName -Raw

    # Replace namespace
    $content = $content -replace 'namespace UnsafeThreadSafeTasks\.', 'namespace MaskedTasks.'
    $content = $content -replace 'namespace UnsafeThreadSafeTasks;', 'namespace MaskedTasks;'
    $content = $content -replace 'namespace UnsafeThreadSafeTasks$', 'namespace MaskedTasks'

    # Replace Execute() method bodies with NotImplementedException
    # Match: public override bool Execute() { ... } — handles nested braces
    $content = [regex]::Replace($content,
        '(public\s+override\s+bool\s+Execute\s*\(\s*\)\s*)\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\}',
        '$1{
        // TODO: Implement the thread-safe version of this task.
        // See the XML doc comment above for a description of what this task does
        // and what thread-safety violation it contains.
        throw new System.NotImplementedException();
    }')

    Set-Content -Path $destFile -Value $content -NoNewline
    Write-Host "  Masked: $relativePath"
}

Write-Host "`nMasked $($csFiles.Count) files into $TargetDir/"
