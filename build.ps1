# build.ps1 - 使用 .NET Framework 的 csc.exe 编译并嵌入 app.ico
param(
    [string]$Source = 'Program.cs',
    [string]$Output = 'URLLauncher.exe'
)

$cwd = Split-Path -Parent $MyInvocation.MyCommand.Definition
if (-not $cwd) { $cwd = Get-Location }

Set-Location -LiteralPath $cwd

$appIcon = Join-Path $cwd 'app.ico'
if (-not (Test-Path $appIcon)) {
    Write-Error "app.ico not found: $appIcon"
    exit 1
}

# 可能的 csc.exe 路径（优先 64 位）
$cscCandidates = @(
    'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe',
    'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe',
    'C:\Windows\Microsoft.NET\Framework64\v3.5\csc.exe',
    'C:\Windows\Microsoft.NET\Framework\v3.5\csc.exe'
)

$csc = $null
foreach ($p in $cscCandidates) {
    if (Test-Path $p) { $csc = $p; break }
}

if (-not $csc) {
    $cmd = Get-Command csc.exe -ErrorAction SilentlyContinue
    if ($cmd) { $csc = $cmd.Source }
}

if (-not $csc) {
    Write-Error "csc.exe not found, cannot compile using .NET Framework. Please install .NET Framework or make csc.exe available in PATH."
    exit 2
}

Write-Host "Using csc: $csc"

# 推荐的引用，确保 System.Web.Extensions 可用（在 .NET Framework 上）
$references = '/r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:Microsoft.CSharp.dll /r:System.Core.dll'

$srcPath = Join-Path $cwd $Source
if (-not (Test-Path $srcPath)) {
    Write-Error "Source file not found: $srcPath"
    exit 3
}

 $args = @(
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    "/win32icon:$appIcon",
    $references,
    "/out:$Output",
    $srcPath
)

Write-Host "Running:" $csc
$proc = Start-Process -FilePath $csc -ArgumentList $args -Wait -NoNewWindow -PassThru
if ($proc.ExitCode -ne 0) {
    Write-Error "Build failed, exit code: $($proc.ExitCode)"
    exit $proc.ExitCode
}
Write-Host "Build succeeded: $Output"
