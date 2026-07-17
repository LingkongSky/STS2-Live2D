[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedVersion,

    [string]$ExpectedCommit = ""
)

$ErrorActionPreference = 'Stop'
$package = (Resolve-Path -LiteralPath $PackagePath).Path

Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Reflection.Metadata
$archive = [IO.Compression.ZipFile]::OpenRead($package)
try {
    $entries = @($archive.Entries)
    $entryNames = @($entries | ForEach-Object { $_.FullName.Replace('\', '/') })

    $requiredEntries = @(
        'README.md',
        'THIRD-PARTY-NOTICES.md',
        'buildTransitive/STS2.Live2D.targets',
        'ref/net9.0/Live2D.dll',
        'ref/net9.0/Live2D.xml'
    )
    foreach ($required in $requiredEntries) {
        if ($required -notin $entryNames) {
            throw "NuGet package is missing required entry: $required"
        }
    }

    foreach ($name in $entryNames) {
        if ($name -match '(^|/)node_modules/' -or
            $name.StartsWith('lib/', [StringComparison]::OrdinalIgnoreCase) -or
            $name.StartsWith('runtimes/', [StringComparison]::OrdinalIgnoreCase)) {
            throw "NuGet package contains a forbidden runtime or local dependency entry: $name"
        }
        if ($name.StartsWith('docs/', [StringComparison]::OrdinalIgnoreCase) -and
            -not $name.StartsWith('docs/content/', [StringComparison]::OrdinalIgnoreCase)) {
            throw "NuGet package contains documentation outside docs/content: $name"
        }
    }

    $nuspecEntries = @($entries | Where-Object { $_.FullName.EndsWith('.nuspec', [StringComparison]::OrdinalIgnoreCase) })
    if ($nuspecEntries.Count -ne 1) {
        throw "Expected exactly one nuspec entry, found $($nuspecEntries.Count)."
    }

    $reader = [IO.StreamReader]::new($nuspecEntries[0].Open())
    try {
        [xml]$nuspec = $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }

    $metadataPath = "/*[local-name()='package']/*[local-name()='metadata']"
    $id = $nuspec.SelectSingleNode("$metadataPath/*[local-name()='id']").InnerText
    $version = $nuspec.SelectSingleNode("$metadataPath/*[local-name()='version']").InnerText
    if ($id -ne 'STS2.Live2D') {
        throw "Unexpected NuGet package ID: $id"
    }
    if ($version -ne $ExpectedVersion) {
        throw "NuGet version '$version' does not match expected version '$ExpectedVersion'."
    }

    $dependencyIds = @(
        $nuspec.SelectNodes("$metadataPath/*[local-name()='dependencies']//*[local-name()='dependency']") |
            ForEach-Object { $_.GetAttribute('id') }
    )
    if ('Godot.SourceGenerators' -in $dependencyIds) {
        throw 'Build-only dependency leaked into the NuGet package: Godot.SourceGenerators'
    }
    $ritsuDependency = $dependencyIds | Where-Object { $_.StartsWith('STS2.RitsuLib.Compat.', [StringComparison]::Ordinal) } | Select-Object -First 1
    if ($null -ne $ritsuDependency) {
        throw "Build-only dependency leaked into the NuGet package: $ritsuDependency"
    }
    if ('GodotSharp' -notin $dependencyIds) {
        throw 'The NuGet package must declare GodotSharp because its public API exposes Godot types.'
    }

    if (-not [string]::IsNullOrWhiteSpace($ExpectedCommit)) {
        $repository = $nuspec.SelectSingleNode("$metadataPath/*[local-name()='repository']")
        $actualCommit = if ($null -eq $repository) { '' } else { $repository.GetAttribute('commit') }
        if ($actualCommit -ne $ExpectedCommit) {
            throw "NuGet repository commit '$actualCommit' does not match expected commit '$ExpectedCommit'."
        }
    }

    $referenceEntry = $entries | Where-Object { $_.FullName.Replace('\', '/') -eq 'ref/net9.0/Live2D.dll' } | Select-Object -First 1
    $referenceStream = $referenceEntry.Open()
    try {
        $memory = [IO.MemoryStream]::new()
        $referenceStream.CopyTo($memory)
        $referenceBytes = $memory.ToArray()
        $referenceText = [Text.Encoding]::UTF8.GetString($referenceBytes)
    }
    finally {
        $referenceStream.Dispose()
        if ($null -ne $memory) { $memory.Dispose() }
    }
    if (-not $referenceText.Contains('ReferenceAssemblyAttribute', [StringComparison]::Ordinal)) {
        throw 'ref/net9.0/Live2D.dll is not a metadata-only reference assembly.'
    }

    $metadataStream = [IO.MemoryStream]::new($referenceBytes, $false)
    $peReader = [Reflection.PortableExecutable.PEReader]::new($metadataStream)
    try {
        $metadata = [Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($peReader)
        $publicTypes = @()
        foreach ($handle in $metadata.TypeDefinitions) {
            $definition = $metadata.GetTypeDefinition($handle)
            $visibility = [int]$definition.Attributes -band [int][Reflection.TypeAttributes]::VisibilityMask
            if ($visibility -ne [int][Reflection.TypeAttributes]::Public) {
                continue
            }

            $namespace = $metadata.GetString($definition.Namespace)
            $name = $metadata.GetString($definition.Name)
            $publicTypes += "$namespace.$name"
        }
    }
    finally {
        $peReader.Dispose()
        $metadataStream.Dispose()
    }
    if ($publicTypes.Count -eq 0) {
        throw 'The reference assembly does not expose any public API types.'
    }
    $unexpectedPublicTypes = @($publicTypes | Where-Object { -not $_.StartsWith('Live2D.Api.', [StringComparison]::Ordinal) })
    if ($unexpectedPublicTypes.Count -gt 0) {
        throw "Types outside Live2D.Api leaked into the public package surface: $($unexpectedPublicTypes -join ', ')"
    }

    Write-Output "NuGet validation passed: $id $version, $($entries.Count) entries, $($publicTypes.Count) public API types."
}
finally {
    $archive.Dispose()
}
