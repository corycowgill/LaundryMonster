# Publish the current Builds/WebGL output to the `deploy` branch that Render serves.
#
# A thin wrapper so Windows users do not have to find bash themselves. The actual work
# lives in publish-deploy.sh, which is also what the Unity menu item runs - one
# implementation of the git dance, not three.
#
#   .\deploy.ps1
#
# Build first: Unity menu > Laundry Monster > Build WebGL, or use
# "Build and Deploy WebGL" to do both in one click.

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $MyInvocation.MyCommand.Path

$candidates = @(
    'C:\Program Files\Git\bin\bash.exe',
    'C:\Program Files (x86)\Git\bin\bash.exe',
    (Join-Path $env:LOCALAPPDATA 'Programs\Git\bin\bash.exe')
)

$bash = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $bash) {
    $cmd = Get-Command bash -ErrorAction SilentlyContinue
    if ($cmd) { $bash = $cmd.Source }
}
if (-not $bash) {
    Write-Error "bash not found. Install Git for Windows, or run ./publish-deploy.sh from Git Bash."
}

& $bash (Join-Path $repo 'publish-deploy.sh')
exit $LASTEXITCODE
