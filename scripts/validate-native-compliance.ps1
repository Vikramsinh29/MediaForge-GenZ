param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path -LiteralPath $Root).Path
$findings = [System.Collections.Generic.List[string]]::new()
$excludedDirectoryNames = @('.git', '.vs', 'bin', 'obj')
$nativeDirectoryNames = @('jniLibs', 'native-libs', 'xcframeworks', 'frameworks')
$nativeExtensions = @(
    '.a', '.aar', '.dylib', '.exe', '.framework', '.lib', '.o', '.obj',
    '.so', '.xcframework'
)

# Assemble policy tokens so this validator does not flag its own source.
$prohibitedFlags = @(
    ('--enable-' + 'gpl'),
    ('--enable-' + 'nonfree'),
    ('--enable-lib' + 'x264'),
    ('--enable-lib' + 'x265'),
    ('--enable-lib' + 'xvid'),
    ('--enable-lib' + 'vidstab'),
    ('--enable-lib' + 'rubberband'),
    ('--enable-lib' + 'fdk-aac')
)

$buildFileExtensions = @(
    '.bat', '.cmake', '.cmd', '.csproj', '.json', '.mk', '.props', '.ps1',
    '.sh', '.targets', '.toml', '.yaml', '.yml'
)
$buildFileNames = @('CMakeLists.txt', 'Dockerfile', 'Makefile')

function Test-IsExcludedPath {
    param([string]$Path)

    $relative = Get-RelativePath -Path $Path
    $segments = $relative -split '[\\/]'
    return $segments | Where-Object { $excludedDirectoryNames -contains $_ } | Select-Object -First 1
}

function Get-RelativePath {
    param([string]$Path)

    $rootPrefix = $rootPath.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if ($Path.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        return $Path.Substring($rootPrefix.Length)
    }

    return $Path
}

function Get-HexPrefix {
    param(
        [string]$Path,
        [int]$Length = 8
    )

    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $buffer = [byte[]]::new($Length)
        $read = $stream.Read($buffer, 0, $buffer.Length)
        if ($read -eq 0) {
            return ''
        }

        return ([BitConverter]::ToString($buffer, 0, $read)).Replace('-', '')
    }
    finally {
        $stream.Dispose()
    }
}

$directories = Get-ChildItem -LiteralPath $rootPath -Directory -Recurse -Force
foreach ($directory in $directories) {
    if (Test-IsExcludedPath -Path $directory.FullName) {
        continue
    }

    if ($nativeDirectoryNames -contains $directory.Name -or
        $directory.Name.EndsWith('.framework', [StringComparison]::OrdinalIgnoreCase) -or
        $directory.Name.EndsWith('.xcframework', [StringComparison]::OrdinalIgnoreCase)) {
        $relative = Get-RelativePath -Path $directory.FullName
        $findings.Add("Unapproved native-library directory: $relative")
    }
}

$files = Get-ChildItem -LiteralPath $rootPath -File -Recurse -Force
foreach ($file in $files) {
    if (Test-IsExcludedPath -Path $file.FullName) {
        continue
    }

    $relative = Get-RelativePath -Path $file.FullName
    $extension = $file.Extension.ToLowerInvariant()

    if ($nativeExtensions -contains $extension) {
        $findings.Add("Unapproved native binary extension: $relative")
    }

    if (($buildFileExtensions -contains $extension -or $buildFileNames -contains $file.Name) -and
        $file.FullName -ne $PSCommandPath) {
        $content = Get-Content -LiteralPath $file.FullName -Raw
        foreach ($flag in $prohibitedFlags) {
            if ($content.IndexOf($flag, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                $findings.Add("Prohibited LGPL-only configuration token '$flag': $relative")
            }
        }
    }

    $prefix = Get-HexPrefix -Path $file.FullName
    $isElf = $prefix.StartsWith('7F454C46', [StringComparison]::Ordinal)
    $isPe = $prefix.StartsWith('4D5A', [StringComparison]::Ordinal)
    $isArchive = $prefix.StartsWith('213C617263683E0A', [StringComparison]::Ordinal)
    $isMachO =
        $prefix.StartsWith('FEEDFACE', [StringComparison]::Ordinal) -or
        $prefix.StartsWith('CEFAEDFE', [StringComparison]::Ordinal) -or
        $prefix.StartsWith('FEEDFACF', [StringComparison]::Ordinal) -or
        $prefix.StartsWith('CFFAEDFE', [StringComparison]::Ordinal) -or
        $prefix.StartsWith('CAFEBABE', [StringComparison]::Ordinal)

    if ($isElf -or $isPe -or $isArchive -or $isMachO) {
        $findings.Add("Unapproved native binary signature: $relative")
    }
}

if ($findings.Count -gt 0) {
    Write-Error ("Native compliance validation failed:`n - " + ($findings -join "`n - "))
    exit 1
}

Write-Output 'Native compliance validation passed: no prohibited flags or native artifacts found.'
exit 0
