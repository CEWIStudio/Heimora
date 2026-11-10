param([string]$Dotnet = 'dotnet', [string]$Inno = 'ISCC.exe', [string]$PackageSource = '')
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.build-home'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot '.packages'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
Push-Location $PSScriptRoot
try {
    $publishArgs = @('publish', 'Heimora.csproj', '-c', 'Release', '-o', 'dist-heimora')
    if ($PackageSource) { $publishArgs += @('--source', $PackageSource) }
    & $Dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    $result = Join-Path $PSScriptRoot 'self-test-result.txt'
    $check = Start-Process -FilePath (Join-Path $PSScriptRoot 'dist-heimora\Heimora.exe') -ArgumentList @('--self-test', $result) -Wait -PassThru -WindowStyle Hidden
    if ($check.ExitCode -ne 0) { Get-Content $result; throw 'Self-test failed' }
    Get-Content $result
    & $Inno installer.iss
    if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }
} finally { Pop-Location }
