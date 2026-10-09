param([string]$OutputDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts\small-text'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$runner = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
function Recognize([string]$ImagePath) {
    $out = $ImagePath + '.txt'
    & $runner -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $root 'ocr.ps1') -InputPath $ImagePath -OutputPath $out -Language zh-CN
    if ($LASTEXITCODE -ne 0) { throw ([IO.File]::ReadAllText($out)) }
    [IO.File]::ReadAllText($out)
}
function Check([bool]$Ok,[string]$Message) { if (!$Ok) { throw $Message }; Write-Output ('通过：'+$Message) }
foreach ($size in @(12,16,32)) {
    $image = New-Object Drawing.Bitmap(600,($size*3+12))
    $graphics = [Drawing.Graphics]::FromImage($image)
    $font = New-Object Drawing.Font('Microsoft YaHei',$size,[Drawing.FontStyle]::Regular,[Drawing.GraphicsUnit]::Pixel)
    try {
        $graphics.Clear([Drawing.Color]::White)
        $graphics.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit
        $graphics.DrawString('小字截图翻译测试 12345',$font,[Drawing.Brushes]::Black,4,4)
        $path = Join-Path $OutputDirectory ('chinese-'+$size+'.png')
        $image.Save($path,[Drawing.Imaging.ImageFormat]::Png)
    } finally { $font.Dispose(); $graphics.Dispose(); $image.Dispose() }
    $actual = (Recognize $path) -replace '\s',''
    Check ($actual -eq '小字截图翻译测试12345') ($size.ToString()+' 像素中文及数字完整识别')
}
$image = New-Object Drawing.Bitmap(300,80)
$graphics = [Drawing.Graphics]::FromImage($image)
try { $graphics.Clear([Drawing.Color]::White); $blank=Join-Path $OutputDirectory 'blank.png'; $image.Save($blank,[Drawing.Imaging.ImageFormat]::Png) }
finally { $graphics.Dispose(); $image.Dispose() }
Check ([string]::IsNullOrWhiteSpace((Recognize $blank))) '空白截图不会产生文字'
