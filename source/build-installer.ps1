param([string]$OutputPath = (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) '随译-安装程序-1.1.0.exe'),[string]$PayloadRoot = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$root = $PayloadRoot
$payload = Join-Path ([IO.Path]::GetTempPath()) ('LinguaDesk-payload-' + [Guid]::NewGuid().ToString('N') + '.zip')
try {
    Compress-Archive -LiteralPath (Join-Path $root 'LinguaDesk.exe'),(Join-Path $root 'ocr.ps1'),(Join-Path $root '使用说明.md'),(Join-Path $root 'language-profiles.json'),(Join-Path $root '第三方许可.txt') -DestinationPath $payload
    $refs = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
    & (Join-Path $framework 'csc.exe') /nologo /target:winexe /platform:x64 /optimize+ ('/win32icon:' + (Join-Path (Split-Path $PSScriptRoot -Parent) 'assets\LinguaDesk.ico')) ('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')) ('/resource:' + $payload + ',payload.zip') ('/out:' + $OutputPath) @refs (Join-Path $PSScriptRoot 'Setup.cs')
    if ($LASTEXITCODE -ne 0) { throw '安装包编译失败' }
    Write-Output $OutputPath
} finally { if (Test-Path -LiteralPath $payload) { Remove-Item -LiteralPath $payload } }
