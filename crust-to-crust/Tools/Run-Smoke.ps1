param(
    [Parameter(Mandatory=$true)][string[]]$Scenes,
    [string]$Godot = 'C:/Users/User/Documents/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe',
    [switch]$Headless
)
$ErrorActionPreference = 'Stop'
$gameRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = Join-Path $gameRoot 'artifacts'
New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
$savePath = Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Godot/app_userdata/Crust-to-Crust/crust_save.cfg'
$saveExisted = Test-Path -LiteralPath $savePath
$saveBytes = if ($saveExisted) { [IO.File]::ReadAllBytes($savePath) } else { $null }
$failed = $false
try {
    foreach ($sceneName in $Scenes) {
        if ($sceneName -notmatch '^[A-Za-z]+Smoke$') { throw "Invalid smoke scene: $sceneName" }
        $stdoutPath = Join-Path $artifactRoot "$sceneName.log"
        $stderrPath = Join-Path $artifactRoot "$sceneName.stderr.log"
        $godotArgs = @('--path', ('"' + $gameRoot + '"'), "res://Tools/$sceneName.tscn", '--audio-driver', 'Dummy', '--max-fps', '60')
        if ($Headless -or $sceneName -eq 'EconomySmoke') { $godotArgs += '--headless' }
        $smokeProcess = Start-Process -FilePath $Godot -ArgumentList $godotArgs -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
        if (-not $smokeProcess.WaitForExit(180000)) { $smokeProcess.Kill(); throw "$sceneName timed out" }
        $smokeProcess.WaitForExit()
        $outputText = [IO.File]::ReadAllText($stdoutPath)
        $errorText = [IO.File]::ReadAllText($stderrPath)
        Write-Output $outputText
        if ($errorText.Length -gt 0) { Write-Output $errorText }
        if ($smokeProcess.ExitCode -ne 0 -or $errorText -match '(?m)^ERROR:') { $failed = $true }
    }
} finally {
    if ($saveExisted) { [IO.File]::WriteAllBytes($savePath,$saveBytes) }
    elseif (Test-Path -LiteralPath $savePath) { Remove-Item -LiteralPath $savePath }
}
if ($failed) { exit 1 }
