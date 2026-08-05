param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectPath
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ProjectPath -PathType Leaf))
{
    throw "The application project was not found at '$ProjectPath'."
}

[xml]$project = Get-Content -LiteralPath $ProjectPath -Raw
$versionNodes = @($project.Project.PropertyGroup.Version | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

if ($versionNodes.Count -ne 1)
{
    throw "SimulcastUtility.csproj must contain exactly one Version property."
}

$version = $versionNodes[0].Trim()

if ($version -notmatch '^\d+\.\d+\.\d+$')
{
    throw "Application version '$version' must use major.minor.patch format."
}

Write-Output $version
