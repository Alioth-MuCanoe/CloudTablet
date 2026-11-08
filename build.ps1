$ErrorActionPreference = 'Stop'
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $dir

$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) {
    throw '找不到 .NET Framework 自带的 csc.exe。请确认系统已安装 .NET Framework 4.x。'
}

if (-not (Test-Path 'app.ico')) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $dir 'make_icon.ps1') -OutPath 'app.ico'
}

$sources = @(
    'Program.cs',
    'AssemblyInfo.cs',
    'Models.cs',
    'Themes.cs',
    'Ui.cs',
    'Native.cs',
    'Brightness.cs',
    'CoverCache.cs',
    'ImageLoader.cs',
    'WebPImageDecoder.cs',
    'BookLoader.cs',
    'EpubLoader.cs',
    'Layout.cs',
    'ReaderForm.cs',
    'ReaderForm.Layout.cs',
    'ReaderForm.Input.cs',
    'ReaderForm.Paint.cs',
    'ReaderForm.Shelf.cs',
    'AboutForm.cs'
)

$fw = Split-Path -Parent $csc
$cscArgs = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/codepage:65001',
    '/out:云牍.exe',
    '/win32icon:app.ico',
    '/win32manifest:app.manifest',
    '/resource:logo-192.png,CloudTablet.logo.png',
    '/r:System.dll',
    '/r:System.Core.dll',
    '/r:System.Drawing.dll',
    '/r:System.Windows.Forms.dll',
    '/r:System.Xml.dll',
    ('/r:' + (Join-Path $fw 'System.IO.Compression.dll')),
    ('/r:' + (Join-Path $fw 'System.IO.Compression.FileSystem.dll')),
    ('/r:' + (Join-Path $fw 'WPF\PresentationCore.dll')),
    ('/r:' + (Join-Path $fw 'WPF\WindowsBase.dll')),
    ('/r:' + (Join-Path $fw 'System.Xaml.dll'))
) + $sources

& $csc $cscArgs
if ($LASTEXITCODE -ne 0) {
    throw "编译失败，退出码 $LASTEXITCODE"
}

$exe = Get-Item '云牍.exe'
Write-Host ("编译完成: {0}  ({1:N1} KB)" -f $exe.Name, ($exe.Length / 1KB))
