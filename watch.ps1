# watch.ps1 - 简单轮询实现：监视 .cs 与 app.ico 文件修改并触发 build.ps1
param(
    [string]$Path = '.',
    [int]$PollSeconds = 1
)

$full = (Resolve-Path -LiteralPath $Path).Path
Set-Location -LiteralPath $full
Write-Host "Watching path: $full (poll every $PollSeconds s). Will run build.ps1 when *.cs or app.ico changes."

$filesToWatch = Get-ChildItem -Path $full -Filter '*.cs' -File -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName }
if (Test-Path (Join-Path $full 'app.ico')) { $filesToWatch += (Join-Path $full 'app.ico') }

$lastWrites = @{}
foreach ($f in $filesToWatch) { if (Test-Path $f) { $lastWrites[$f] = (Get-Item $f).LastWriteTimeUtc } }

while ($true) {
    Start-Sleep -Seconds $PollSeconds
    $changed = $false
    $currentFiles = Get-ChildItem -Path $full -Filter '*.cs' -File -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName }
    if (Test-Path (Join-Path $full 'app.ico')) { $currentFiles += (Join-Path $full 'app.ico') }

    foreach ($f in $currentFiles | Sort-Object -Unique) {
        $exists = Test-Path $f
        if (-not $exists) { if ($lastWrites.ContainsKey($f)) { $lastWrites.Remove($f); $changed = $true } ; continue }
        $t = (Get-Item $f).LastWriteTimeUtc
        if (-not $lastWrites.ContainsKey($f)) { $lastWrites[$f] = $t; continue }
        if ($t -ne $lastWrites[$f]) { $lastWrites[$f] = $t; $changed = $true }
    }

    if ($changed) {
        Start-Sleep -Milliseconds 600
        Write-Host "Change detected at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss'). Running build.ps1..."
        try {
            & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $full 'build.ps1')
        } catch {
            Write-Error $_
        }
    }
}
