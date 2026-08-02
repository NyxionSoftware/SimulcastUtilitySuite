param(
    [Parameter(Mandatory = $true)]
    [string]$InputFile
)

$ErrorActionPreference = "Stop"

try
{
    $receivers = @(Get-Content -LiteralPath $InputFile -Raw | ConvertFrom-Json)
}
catch
{
    exit 1
}

if ($receivers.Count -eq 0)
{
    exit 2
}

$receiverIds = [System.Collections.Generic.HashSet[guid]]::new()

foreach ($receiver in $receivers)
{
    $parsedId = [guid]::Empty
    $parsedIpAddress = $null
    $octets = ([string]$receiver.IpAddress).Split('.')

    if (-not [guid]::TryParse([string]$receiver.Id, [ref]$parsedId) -or $parsedId -eq [guid]::Empty -or -not $receiverIds.Add($parsedId))
    {
        exit 3
    }

    if ([string]::IsNullOrWhiteSpace([string]$receiver.Name) -or [string]$receiver.ReceiverId -notmatch '^\d+$')
    {
        exit 3
    }

    if ($octets.Count -ne 4 -or $octets.Where({ $_ -notmatch '^\d{1,3}$' -or [int]$_ -gt 255 }).Count -ne 0 -or -not [System.Net.IPAddress]::TryParse([string]$receiver.IpAddress, [ref]$parsedIpAddress) -or $parsedIpAddress.AddressFamily -ne [System.Net.Sockets.AddressFamily]::InterNetwork)
    {
        exit 3
    }
}

exit 0
