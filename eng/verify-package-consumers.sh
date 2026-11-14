#!/bin/sh
set -eu

version="$1"
major="$2"
export DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0 DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1

# Each container has only its SDK's runtime. Installation must select a runnable payload.
dotnet --list-runtimes
if dotnet --list-runtimes | grep -q "Microsoft.NETCore.App $([ "$major" = 10 ] && echo 11 || echo 10)\."; then
    echo 'Consumer validation requires an isolated runtime installation.' >&2
    exit 1
fi
dotnet tool install PinguApps.RazorStyle.Cli --version "$version" --tool-path /tmp/razorstyle-tool --add-source /packages

frameworks=10
if [ "$major" = 11 ]; then frameworks='10 11'; fi
for framework in $frameworks; do
    work="/tmp/consumer-$framework"
    mkdir -p "$work"
    cd "$work"
    cat > Consumer.csproj <<EOF
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup><TargetFramework>net$framework.0</TargetFramework><RootNamespace>Consumer</RootNamespace><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App" /></ItemGroup>
</Project>
EOF
    printf '<div id="sample" class="first">text</div>\n' > Example.razor
    cat > Example.razor.cs <<'EOF'
using Microsoft.AspNetCore.Components;
namespace Consumer;
public partial class Example
{
    private void Work() { }
    [Parameter] public int Value { get; set; }
    private class Nested { }
}
EOF
    dotnet restore Consumer.csproj --disable-build-servers
    code=0
    /tmp/razorstyle-tool/razorstyle check . > cli.log 2>&1 || code=$?
    cat cli.log
    test "$code" = 1
    grep -q PARS0004 cli.log
    grep -q PARS0005 cli.log

    # Restore the actual build package, then exercise its formatter and compiler analyzers.
    sed -i "s|</Project>|<PropertyGroup><RazorStyleCommand>fix</RazorStyleCommand><DisablePARS0004>true</DisablePARS0004><DisablePARS0005>true</DisablePARS0005></PropertyGroup><ItemGroup><PackageReference Include=\"PinguApps.RazorStyle\" Version=\"$version\" /></ItemGroup></Project>|" Consumer.csproj
    dotnet restore Consumer.csproj --source /packages --source https://api.nuget.org/v3/index.json --disable-build-servers
    selected=$(dotnet msbuild Consumer.csproj -getProperty:RazorStyleCliPath)
    case "$selected" in */tools/net"$major".0/PinguApps.RazorStyle.Cli.dll) ;; *) echo "Wrong formatter: $selected" >&2; exit 1 ;; esac
    override=$(dotnet msbuild Consumer.csproj -getProperty:RazorStyleCliPath -p:RazorStyleCliPath=/custom/formatter.dll)
    test "$override" = /custom/formatter.dll
    cp Example.razor before.razor.txt
    dotnet build Consumer.csproj -c Release --no-restore --disable-build-servers
    if cmp -s before.razor.txt Example.razor; then echo 'Build did not format markup.' >&2; exit 1; fi
    /tmp/razorstyle-tool/razorstyle check . --disable PARS0004 --disable PARS0005
    echo "PASS: SDK $major, net$framework.0 consumer, installed CLI and embedded net$major.0 formatter."
done
