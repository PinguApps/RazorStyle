[CmdletBinding()]
param([string]$PackageVersion = '0.1.0-compatibility')

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$dotnet = (Get-Command dotnet -CommandType Application).Source
$docker = (Get-Command docker -CommandType Application).Source
$root = Split-Path $PSScriptRoot -Parent
$feed = Join-Path $root 'artifacts/compatibility-packages'
& $dotnet pack (Join-Path $root 'PinguApps.RazorStyle.slnx') -c Release --no-build "-p:PackageVersion=$PackageVersion" -o $feed
foreach ($sdk in '10.0.404', '11.0.100') {
    & $docker run --rm --mount "type=bind,source=$feed,target=/packages,readonly" `
        --mount "type=bind,source=$PSScriptRoot,target=/verification,readonly" `
        "mcr.microsoft.com/dotnet/sdk:$sdk" sh /verification/verify-package-consumers.sh $PackageVersion ($sdk.Split('.')[0])
}
