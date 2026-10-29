param(
[Parameter(Mandatory=$true)]
[string]
$version,

[string]
$baseOutputPath = 'D:\Proyectos\Builds'
)

# Single self-contained Windows x64 build (does not require .NET to be installed on the server)
.\buildserver.ps1 -version $version -baseOutputPath $baseOutputPath -os win-x64 -sc true
