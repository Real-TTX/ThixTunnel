<#
.SYNOPSIS
    End-to-end test of a real two-hop tunnel against Docker containers.

.DESCRIPTION
    Starts on a private Docker network:
      hop1  SSH server, reachable from Windows on 127.0.0.1:22221   (jump server, "Profil 1")
      hop2  SSH server, only reachable from hop1                    (last SSH server, "Profil 2")
      web   nginx, only reachable from hop2 as web:80               (the target)
    Then runs the tests marked E2E (tests/ThixTunnel.Tests/EndToEndTests.cs) with real ssh.exe and cleans up.
    The user's ~/.ssh is not touched: key and known_hosts live in a temporary folder.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\e2e-docker.ps1
#>
param([switch] $KeepContainers)

# Native tools (docker, ssh-keygen) write progress to stderr; failures are checked through $LASTEXITCODE.
$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $PSScriptRoot
$work = Join-Path ([System.IO.Path]::GetTempPath()) "thixtunnel-e2e-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
New-Item -ItemType Directory $work | Out-Null
$network = 'thixtunnel-e2e'
$ssh = Join-Path $env:SystemRoot 'System32\OpenSSH'

function Remove-Environment {
    $ErrorActionPreference = 'SilentlyContinue'
    docker rm -f thixtunnel-e2e-hop1 thixtunnel-e2e-hop2 thixtunnel-e2e-web 2>$null | Out-Null
    docker network rm $network 2>$null | Out-Null
}

try {
    Remove-Environment

    # Key pair for both servers (no passphrase).
    & (Join-Path $ssh 'ssh-keygen.exe') -q -t ed25519 -N '""' -C thixtunnel-e2e -f (Join-Path $work 'id_e2e') | Out-Null
    if (-not (Test-Path (Join-Path $work 'id_e2e'))) { & (Join-Path $ssh 'ssh-keygen.exe') -q -t ed25519 -N '' -C thixtunnel-e2e -f (Join-Path $work 'id_e2e') | Out-Null }
    $build = Join-Path $work 'image'
    New-Item -ItemType Directory $build | Out-Null
    Copy-Item (Join-Path $root 'tests\e2e\Dockerfile') $build
    Copy-Item (Join-Path $work 'id_e2e.pub') (Join-Path $build 'authorized_keys')
    docker build -q -t thixtunnel-e2e-sshd $build | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'docker build failed' }

    docker network create $network | Out-Null
    docker run -d --name thixtunnel-e2e-hop1 --network $network --network-alias hop1 -p 127.0.0.1:22221:22 thixtunnel-e2e-sshd | Out-Null
    docker run -d --name thixtunnel-e2e-hop2 --network $network --network-alias hop2 thixtunnel-e2e-sshd | Out-Null
    docker run -d --name thixtunnel-e2e-web --network $network --network-alias web nginx:alpine | Out-Null
    Start-Sleep -Seconds 3

    # known_hosts of the test: hop1 as seen from Windows, hop2 as seen through hop1.
    $knownHosts = Join-Path $work 'known_hosts'
    $hop1Key = (docker exec thixtunnel-e2e-hop1 cat /etc/ssh/ssh_host_ed25519_key.pub).Split(' ')[0..1] -join ' '
    $hop2Key = (docker exec thixtunnel-e2e-hop2 cat /etc/ssh/ssh_host_ed25519_key.pub).Split(' ')[0..1] -join ' '
    Set-Content -Path $knownHosts -Encoding ascii -Value @("[127.0.0.1]:22221 $hop1Key", "hop2 $hop2Key")

    $env:THIXTUNNEL_E2E_KEY = Join-Path $work 'id_e2e'
    $env:THIXTUNNEL_E2E_KNOWN_HOSTS = $knownHosts
    dotnet test (Join-Path $root 'tests\ThixTunnel.Tests\ThixTunnel.Tests.csproj') --filter 'Category=E2E' --logger 'console;verbosity=normal'
    $testExit = $LASTEXITCODE
}
finally {
    Remove-Item Env:THIXTUNNEL_E2E_KEY, Env:THIXTUNNEL_E2E_KNOWN_HOSTS -ErrorAction SilentlyContinue
    if (-not $KeepContainers) { Remove-Environment }
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}

exit $testExit
