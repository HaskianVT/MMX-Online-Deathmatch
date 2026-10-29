param(
[Parameter(Mandatory=$true)]
[string]
$version,

[string]
$baseOutputPath = 'D:\Proyectos\Builds',

[string]
[ValidateSet('win-x86', 'win-x64', 'osx-x64', 'debian-x64')]
$os = 'win-x64',

[string]
[ValidateSet('false', 'true')]
$sc = 'true'
)

$ErrorActionPreference = "Stop"

$versionFileName = $version.Replace('.','_')
$osStr = $os.Replace('-','_')
$scStr = If ($sc -eq 'true') {"_sc"} Else {""}
$outputPath = "$baseOutputPath\RelayServerV$versionFileName" + "_" + $osStr + $scStr

$content = Get-Content -path "MMX\Global.cs"
$content -Replace 'public static decimal version = (.+)m', "public static decimal version = $($version)m" |  Out-File "MMX\Global.cs"

$constant = "WINDOWS"
If (($os -eq 'debian-x64') -or ($os -eq 'osx-x64'))
{
    $constant = "LINUX"
}

dotnet publish .\RelayServer\RelayServer.csproj -o $outputPath -c Release -r $os --self-contained $sc -p:DefineConstants="$constant%3BRELAYSERVER"
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit code $LASTEXITCODE)" }

# Copy over misc game files
Copy-Item -Path .\GameFiles\overrideversion.txt -Destination $outputPath -Force
Copy-Item -Path .\GameFiles\RelayServerHelp.url -Destination $outputPath -Force

# Secrets are generated once and kept outside the build folders, so every build (and the
# Ban Tool / Server Tools, which need the same secretPrefix.txt) keeps using the same values.
$secretsPath = "$baseOutputPath\RelayServerSecrets"
New-Item -ItemType Directory -Path $secretsPath -Force | Out-Null

function New-RandomHex([int]$byteCount)
{
    $bytes = New-Object byte[] $byteCount
    $rng = New-Object System.Security.Cryptography.RNGCryptoServiceProvider
    $rng.GetBytes($bytes)
    $rng.Dispose()
    return -join ($bytes | ForEach-Object { $_.ToString("x2") })
}

# encryptionKey.txt is used as an AES key, so it must be exactly 16, 24 or 32 characters. 16 random bytes -> 32 hex chars (AES-256).
# secretPrefix.txt can be any non-empty string.
$secrets = @{ "encryptionKey.txt" = 16; "secretPrefix.txt" = 16 }
foreach ($fileName in $secrets.Keys)
{
    $secretFile = "$secretsPath\$fileName"
    if (-not (Test-Path $secretFile))
    {
        # No BOM or trailing newline: the relay server reads the file content verbatim
        [IO.File]::WriteAllText($secretFile, (New-RandomHex $secrets[$fileName]))
        Write-Host "Generated $secretFile"
    }
    Copy-Item -Path $secretFile -Destination $outputPath -Force
}
