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

if (-not (Test-Path '云牍.exe')) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $dir 'build.ps1')
}
if (-not (Test-Path '卸载云牍.exe')) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $dir 'build_uninstaller.ps1')
}
if (-not (Test-Path '云牍.exe')) {
    throw '主程序 云牍.exe 不存在，无法制作安装包。'
}
if (-not (Test-Path '卸载云牍.exe')) {
    throw '卸载程序 卸载云牍.exe 不存在，无法制作安装包。'
}
if (-not (Test-Path '使用说明.md')) {
    throw '使用说明.md 不存在。'
}

$cscArgs = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/codepage:65001',
    '/out:云牍一键安装.exe',
    '/win32icon:app.ico',
    '/win32manifest:app.manifest',
    '/resource:云牍.exe,CloudTablet.App.exe',
    '/resource:卸载云牍.exe,CloudTablet.Uninstall.exe',
    '/resource:使用说明.md,CloudTablet.Readme.md',
    '/r:System.dll',
    '/r:System.Core.dll',
    '/r:System.Drawing.dll',
    '/r:System.Windows.Forms.dll',
    'Setup.cs',
    'SetupAssemblyInfo.cs'
)

& $csc $cscArgs
if ($LASTEXITCODE -ne 0) {
    throw "安装包编译失败，退出码 $LASTEXITCODE"
}

$exe = Get-Item '云牍一键安装.exe'
Write-Host ("一键安装包完成: {0}  ({1:N1} KB)" -f $exe.Name, ($exe.Length / 1KB))
