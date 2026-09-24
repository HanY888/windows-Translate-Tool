$ErrorActionPreference = 'Stop'
$dist = Join-Path $PSScriptRoot 'dist'
$app = Join-Path $dist 'app'
New-Item -ItemType Directory -Force $app | Out-Null
& (Join-Path $PSScriptRoot 'source\build.ps1') -OutputPath (Join-Path $app 'LinguaDesk.exe')
foreach ($name in @('ocr.ps1','language-profiles.json','使用说明.md','第三方许可.txt')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $app $name) -Force
}
& (Join-Path $PSScriptRoot 'source\build-installer.ps1') -PayloadRoot $app -OutputPath (Join-Path $dist 'LinguaDesk-Setup-1.2.0.exe')

