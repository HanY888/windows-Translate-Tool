$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$testDir = Join-Path $PSScriptRoot 'artifacts\tests'
New-Item -ItemType Directory -Force $testDir | Out-Null
$refs = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Net.Http.dll','System.Web.Extensions.dll','System.Security.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Xml.dll','System.Xml.Linq.dll','WPF\UIAutomationClient.dll','WPF\UIAutomationTypes.dll','WPF\WindowsBase.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$executable = Join-Path $testDir 'HotkeyTests.exe'
& (Join-Path $framework 'csc.exe') /nologo /target:exe /platform:x64 /main:LinguaDesk.HotkeyTests ('/out:' + $executable) ('/win32icon:' + (Join-Path $PSScriptRoot 'assets\LinguaDesk.ico')) @refs (Join-Path $PSScriptRoot 'source\LinguaDesk.cs') (Join-Path $PSScriptRoot 'source\Hotkeys.cs') (Join-Path $PSScriptRoot 'source\HotkeyTests.cs')
if ($LASTEXITCODE -ne 0) { throw '测试编译失败' }
& $executable
if ($LASTEXITCODE -ne 0) { throw '快捷键测试未通过' }
