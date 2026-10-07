param([string]$Version = '0.2.1')
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$artifactRoot = Join-Path $projectRoot 'artifacts'
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
foreach ($variant in @('framework-dependent', 'self-contained')) {
    $outputPath = Join-Path $artifactRoot "v$Version/$variant"
    $selfContained = if ($variant -eq 'self-contained') { 'true' } else { 'false' }
    & dotnet publish (Join-Path $projectRoot 'JMDataHelper.csproj') -c Release -r win-x64 --self-contained $selfContained '-p:PublishSingleFile=true' '-p:IncludeNativeLibrariesForSelfExtract=true' "-p:EnableCompressionInSingleFile=$selfContained" '-p:DebugType=None' "-p:Version=$Version" -o $outputPath
    if ($LASTEXITCODE -ne 0) { throw "Publish failed: $variant" }
    Copy-Item -LiteralPath (Join-Path $projectRoot 'INSTALL.txt') -Destination $outputPath
    $zipPath = Join-Path $artifactRoot "JMDataHelper-v$Version-win-x64-$variant.zip"
    Compress-Archive -Path (Join-Path $outputPath '*') -DestinationPath $zipPath -Force
    $bytes = (Get-ChildItem -LiteralPath $outputPath -File | Measure-Object Length -Sum).Sum
    [pscustomobject]@{Variant=$variant; FolderMB=[math]::Round($bytes/1MB,2); ZipMB=[math]::Round((Get-Item -LiteralPath $zipPath).Length/1MB,2); Zip=$zipPath}
}
