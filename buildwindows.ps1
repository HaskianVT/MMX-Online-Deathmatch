param(
[Parameter(Mandatory=$true)]
[string]
$version,

[string]
$baseOutputPath = 'D:\Proyectos\Builds',

# Copied into each build so players can connect without setting it up. Skipped if the file doesn't exist.
[string]
$regionFile = "$baseOutputPath\region.txt"
)

$ErrorActionPreference = "Stop"

$builds = @(
    @{ os = 'win-x86'; sc = 'false' },
    @{ os = 'win-x64'; sc = 'true' },
    @{ os = 'win-x64'; sc = 'false' }
)

$includeRegion = Test-Path $regionFile
if (-not $includeRegion)
{
    Write-Host "No region.txt found at $regionFile, builds will not include one."
}

foreach ($build in $builds)
{
    # Each build folder is complete (game, assets and region.txt) so it can be zipped and shared as is
    .\build.ps1 -version $version -baseOutputPath $baseOutputPath -os $build.os -sc $build.sc -includeAssets true -runBuildTools false

    if ($includeRegion)
    {
        $scStr = If ($build.sc -eq 'true') {"_sc"} Else {""}
        $name = "MMXOnlineV$($version.Replace('.','_'))_$($build.os.Replace('-','_'))$scStr"
        Copy-Item -Path $regionFile -Destination "$baseOutputPath\$name\region.txt" -Force
    }
}
