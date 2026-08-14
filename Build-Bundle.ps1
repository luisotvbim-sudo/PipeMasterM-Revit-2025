[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release", "Test")]
    [string]$Configuration = "Test",

    [string]$DotNetPath = "dotnet",

    [string]$RevitInstallDir = "C:\Program Files\Autodesk\Revit 2025",

    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$solution = Join-Path $repoRoot "PipeMasterM.sln"
$project = Join-Path $repoRoot "PipeMasterM.csproj"
$iconsSource = Join-Path $repoRoot "Assets\Icones"
$packageSource = Join-Path $repoRoot "Packaging\PackageContents.xml"
$addinSource = Join-Path $repoRoot "Packaging\PipeMasterM.addin"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "dist"
}
$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
$bundleRoot = Join-Path $outputRoot "PipeMasterM.bundle"
$contents2025 = Join-Path $bundleRoot "Contents\2025"
$iconsDestination = Join-Path $bundleRoot "Contents\Icones"
$buildOutput = Join-Path $repoRoot "bin\$Configuration\net8.0-windows"

if (-not (Test-Path -LiteralPath $solution)) { throw "Solução não encontrada: $solution" }
if (-not (Test-Path -LiteralPath $project)) { throw "Projeto não encontrado: $project" }
if (-not (Test-Path -LiteralPath $iconsSource)) { throw "Ícones não encontrados: $iconsSource" }
if (-not (Test-Path -LiteralPath $packageSource)) { throw "PackageContents.xml não encontrado." }
if (-not (Test-Path -LiteralPath $addinSource)) { throw "Manifesto .addin não encontrado." }
if (-not (Test-Path -LiteralPath (Join-Path $RevitInstallDir "RevitAPI.dll"))) { throw "RevitAPI.dll não encontrada em $RevitInstallDir" }

if (Test-Path -LiteralPath $DotNetPath) {
    $dotnet = (Resolve-Path -LiteralPath $DotNetPath).Path
} else {
    $dotnetCommand = Get-Command $DotNetPath -ErrorAction Stop
    $dotnet = $dotnetCommand.Source
}

& $dotnet restore $solution --verbosity minimal "-p:RevitInstallDir=$RevitInstallDir"
if ($LASTEXITCODE -ne 0) { throw "Falha no restore." }

& $dotnet build $solution -c $Configuration --no-restore --no-incremental -v:minimal '-clp:ErrorsOnly;Summary' "-p:RevitInstallDir=$RevitInstallDir"
if ($LASTEXITCODE -ne 0) { throw "Falha na compilação." }

if (-not (Test-Path -LiteralPath (Join-Path $buildOutput "PipeMasterM.dll"))) {
    throw "A DLL compilada não foi encontrada em $buildOutput"
}

$bundleFull = [System.IO.Path]::GetFullPath($bundleRoot)
$outputPrefix = $outputRoot.TrimEnd('\') + '\'
if (-not $bundleFull.StartsWith($outputPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Destino do bundle fora do diretório de saída: $bundleFull"
}

if (Test-Path -LiteralPath $bundleRoot) {
    Remove-Item -LiteralPath $bundleRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $contents2025 -Force | Out-Null
New-Item -ItemType Directory -Path $iconsDestination -Force | Out-Null

$runtimeDestination = Join-Path $contents2025 "runtimes\win-x64\native"
New-Item -ItemType Directory -Path $runtimeDestination -Force | Out-Null

$runtimeFiles = @(
    "PipeMasterM.dll",
    "PipeMasterM.deps.json",
    "Microsoft.Web.WebView2.Core.dll",
    "Microsoft.Web.WebView2.Wpf.dll"
)
foreach ($relativePath in $runtimeFiles) {
    $sourcePath = Join-Path $buildOutput $relativePath
    if (-not (Test-Path -LiteralPath $sourcePath)) {
        throw "Arquivo de runtime ausente: $sourcePath"
    }
    Copy-Item -LiteralPath $sourcePath -Destination $contents2025
}

$webViewLoader = Join-Path $buildOutput "runtimes\win-x64\native\WebView2Loader.dll"
if (-not (Test-Path -LiteralPath $webViewLoader)) {
    throw "WebView2Loader.dll ausente: $webViewLoader"
}
Copy-Item -LiteralPath $webViewLoader -Destination $runtimeDestination

Copy-Item -LiteralPath $packageSource -Destination (Join-Path $bundleRoot "PackageContents.xml")
Copy-Item -LiteralPath $addinSource -Destination (Join-Path $contents2025 "PipeMasterM.addin")
Get-ChildItem -LiteralPath $iconsSource -File | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $iconsDestination
}

$requiredFiles = @(
    "PackageContents.xml",
    "Contents\2025\PipeMasterM.addin",
    "Contents\2025\PipeMasterM.dll",
    "Contents\2025\PipeMasterM.deps.json",
    "Contents\2025\Microsoft.Web.WebView2.Core.dll",
    "Contents\2025\Microsoft.Web.WebView2.Wpf.dll",
    "Contents\2025\runtimes\win-x64\native\WebView2Loader.dll",
    "Contents\Icones\acesso.png"
)

$missing = @($requiredFiles | Where-Object { -not (Test-Path -LiteralPath (Join-Path $bundleRoot $_)) })
if ($missing.Count -gt 0) {
    throw "Bundle incompleto. Arquivos ausentes: $($missing -join ', ')"
}

$manifestPath = Join-Path $bundleRoot "bundle-manifest.sha256"
$hashLines = Get-ChildItem -LiteralPath $bundleRoot -Recurse -File |
    Where-Object { $_.FullName -ne $manifestPath } |
    Sort-Object FullName |
    ForEach-Object {
        $relative = $_.FullName.Substring($bundleRoot.Length + 1)
        $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        "$hash  $relative"
    }
$hashLines | Set-Content -LiteralPath $manifestPath -Encoding UTF8

Write-Output "Bundle criado com sucesso: $bundleRoot"
Write-Output "Configuração: $Configuration"
Write-Output "Arquivos: $(@(Get-ChildItem -LiteralPath $bundleRoot -Recurse -File).Count)"
