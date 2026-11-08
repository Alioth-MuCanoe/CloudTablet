$ErrorActionPreference = 'Stop'
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $dir

$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) {
    throw '找不到 .NET Framework 自带的 csc.exe。'
}

$cscArgs = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/codepage:65001',
    '/out:卸载云牍.exe',
    '/win32icon:app.ico',
    '/win32manifest:app.manifest',
    '/r:System.dll',
    '/r:System.Drawing.dll',
    '/r:System.Windows.Forms.dll',
    'Uninstall.cs',
    'UninstallAssemblyInfo.cs'
)

& $csc $cscArgs
if ($LASTEXITCODE -ne 0) {
    throw "卸载程序编译失败，退出码 $LASTEXITCODE"
}

$exe = Get-Item '卸载云牍.exe'
Write-Host ("卸载程序完成: {0}  ({1:N1} KB)" -f $exe.Name, ($exe.Length / 1KB))
