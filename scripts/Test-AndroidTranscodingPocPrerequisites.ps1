param(
    [string]$AndroidSdkDirectory = (Join-Path $env:LOCALAPPDATA 'Android\Sdk'),
    [string]$JavaSdkDirectory = 'C:\jdk',
    [string]$FfmpegSourceRoot,
    [string]$ExpectedCommit,
    [string]$ExpectedSourceSha256,
    [string]$ExternalArtifactRoot =
        (Join-Path $env:LOCALAPPDATA 'MediaForgeGenZ\native-poc')
)

$ErrorActionPreference = 'Stop'
$failures = [System.Collections.Generic.List[string]]::new()

function Add-Missing {
    param([string]$Message)
    $failures.Add($Message)
}

if (-not (Test-Path -LiteralPath $AndroidSdkDirectory -PathType Container)) {
    Add-Missing "Android SDK directory is missing: $AndroidSdkDirectory"
}

$ndkRoot = Join-Path $AndroidSdkDirectory 'ndk'
$ndkVersions = @()
if (Test-Path -LiteralPath $ndkRoot -PathType Container) {
    $ndkVersions = @(Get-ChildItem -LiteralPath $ndkRoot -Directory)
}
if ($ndkVersions.Count -eq 0) {
    Add-Missing "Install and pin one Android NDK revision under: $ndkRoot"
}

$cmakeRoot = Join-Path $AndroidSdkDirectory 'cmake'
$cmakeVersions = @()
if (Test-Path -LiteralPath $cmakeRoot -PathType Container) {
    $cmakeVersions = @(Get-ChildItem -LiteralPath $cmakeRoot -Directory)
}
if ($cmakeVersions.Count -eq 0) {
    Add-Missing "Install and pin one Android SDK CMake revision under: $cmakeRoot"
}

if (-not (Test-Path -LiteralPath $JavaSdkDirectory -PathType Container)) {
    Add-Missing "JDK directory is missing: $JavaSdkDirectory"
}

if ([string]::IsNullOrWhiteSpace($FfmpegSourceRoot) -or
    -not (Test-Path -LiteralPath $FfmpegSourceRoot -PathType Container)) {
    Add-Missing 'Provide a developer-supplied FFmpeg source tree outside the repository.'
}

if ($ExpectedCommit -notmatch '^[0-9a-fA-F]{40}$') {
    Add-Missing 'Provide the exact 40-character FFmpeg source commit.'
}

if ($ExpectedSourceSha256 -notmatch '^[0-9a-fA-F]{64}$') {
    Add-Missing 'Provide the verified 64-character FFmpeg source archive SHA-256.'
}

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$artifactFullPath = [System.IO.Path]::GetFullPath($ExternalArtifactRoot)
$repositoryPrefix = $repositoryRoot.TrimEnd('\', '/') +
    [System.IO.Path]::DirectorySeparatorChar
if ($artifactFullPath.StartsWith(
        $repositoryPrefix,
        [StringComparison]::OrdinalIgnoreCase)) {
    Add-Missing 'The native artifact root must be outside the Git repository.'
}

if ($failures.Count -gt 0) {
    Write-Output 'Android transcoding proof prerequisites: BLOCKED'
    foreach ($failure in $failures) {
        Write-Output " - $failure"
    }
    exit 1
}

Write-Output 'Android transcoding proof prerequisites: READY FOR MANUAL REVIEW'
Write-Output " NDK: $($ndkVersions[-1].FullName)"
Write-Output " CMake: $($cmakeVersions[-1].FullName)"
Write-Output " External artifacts: $artifactFullPath"
Write-Output 'No dependency or binary was downloaded by this check.'
exit 0
