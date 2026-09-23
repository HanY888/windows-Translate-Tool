param([string]$OutputPath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'LinguaDesk.exe'))
$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$root = Split-Path $PSScriptRoot -Parent
$references = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Net.Http.dll','System.Web.Extensions.dll','System.Security.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Xml.dll','System.Xml.Linq.dll','WPF\UIAutomationClient.dll','WPF\UIAutomationTypes.dll','WPF\WindowsBase.dll')
$arguments = @('/nologo','/target:winexe','/platform:x64','/optimize+',('/win32icon:' + (Join-Path $root 'assets\LinguaDesk.ico')),('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')),('/out:' + $OutputPath))
foreach ($reference in $references) { $arguments += '/reference:' + (Join-Path $framework $reference) }
$arguments += Join-Path $PSScriptRoot 'LinguaDesk.cs'
$arguments += Join-Path $PSScriptRoot 'Hotkeys.cs'
& (Join-Path $framework 'csc.exe') @arguments
if ($LASTEXITCODE -ne 0) { throw '编译失败' }
Write-Output '已生成 LinguaDesk.exe'
