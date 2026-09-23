param([Parameter(Mandatory=$true)][string]$InputPath, [Parameter(Mandatory=$true)][string]$OutputPath, [string]$Language = 'auto')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Storage.StorageFile,Windows.Storage,ContentType=WindowsRuntime]
$null = [Windows.Graphics.Imaging.BitmapDecoder,Windows.Foundation,ContentType=WindowsRuntime]
$null = [Windows.Media.Ocr.OcrEngine,Windows.Foundation,ContentType=WindowsRuntime]
$null = [Windows.Data.Pdf.PdfDocument,Windows.Data.Pdf,ContentType=WindowsRuntime]
$null = [Windows.Storage.Streams.InMemoryRandomAccessStream,Windows.Storage.Streams,ContentType=WindowsRuntime]
$null = [Windows.Globalization.Language,Windows.Globalization,ContentType=WindowsRuntime]
$null = [Windows.Foundation.IAsyncAction,Windows.Foundation,ContentType=WindowsRuntime]
$script:asTask = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' } | Select-Object -First 1
function Await($Operation, [Type]$Type) {
    $task = $script:asTask.MakeGenericMethod($Type).Invoke($null,@($Operation))
    $task.GetAwaiter().GetResult()
}
function AwaitAction($Operation) {
    $method = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and !$_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncAction' } | Select-Object -First 1
    $task = $method.Invoke($null,@($Operation))
    $null = $task.GetAwaiter().GetResult()
}
function ReadImage($Stream) {
    $decoder = Await ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($Stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
    $limit = [Windows.Media.Ocr.OcrEngine]::MaxImageDimension
    $transform = New-Object Windows.Graphics.Imaging.BitmapTransform
    $scale = [Math]::Min(1.0, ($limit - 1) / [Math]::Max($decoder.PixelWidth,$decoder.PixelHeight))
    $transform.ScaledWidth = [uint32][Math]::Max(1,[Math]::Floor($decoder.PixelWidth * $scale))
    $transform.ScaledHeight = [uint32][Math]::Max(1,[Math]::Floor($decoder.PixelHeight * $scale))
    $bitmap = Await ($decoder.GetSoftwareBitmapAsync([Windows.Graphics.Imaging.BitmapPixelFormat]::Bgra8,[Windows.Graphics.Imaging.BitmapAlphaMode]::Premultiplied,$transform,[Windows.Graphics.Imaging.ExifOrientationMode]::RespectExifOrientation,[Windows.Graphics.Imaging.ColorManagementMode]::DoNotColorManage)) ([Windows.Graphics.Imaging.SoftwareBitmap])
    try {
        $result = Await ($script:engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult])
        $lines = foreach ($line in $result.Lines) {
            $lineText = New-Object Text.StringBuilder
            $previous = $null
            foreach ($word in $line.Words) {
                if ($previous) {
                    $gap = $word.BoundingRect.X - ($previous.BoundingRect.X + $previous.BoundingRect.Width)
                    $tight = $gap -lt ([Math]::Min($previous.BoundingRect.Height,$word.BoundingRect.Height) * 0.15)
                    $bothCjk = $previous.Text -match '[\u4e00-\u9fff]$' -and $word.Text -match '^[\u4e00-\u9fff]'
                    if (!$tight -and !$bothCjk) { $null = $lineText.Append(' ') }
                }
                $null = $lineText.Append($word.Text)
                $previous = $word
            }
            $lineText.ToString()
        }
        return ($lines -join "`r`n")
    } finally { $bitmap.Dispose() }
}
try {
    $script:engine = $null
    if ($Language -ne 'auto') {
        $tag = switch ($Language) { 'zh-CN' { 'zh-Hans-CN' }; 'zh-TW' { 'zh-Hant-TW' }; default { $Language } }
        $lang = New-Object Windows.Globalization.Language($tag)
        if ([Windows.Media.Ocr.OcrEngine]::IsLanguageSupported($lang)) { $script:engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage($lang) }
    }
    if (!$script:engine) { $script:engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages() }
    if (!$script:engine) {
        $available = @([Windows.Media.Ocr.OcrEngine]::AvailableRecognizerLanguages)
        if ($available.Count) { $script:engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage($available[0]) }
    }
    if (!$script:engine) { throw 'Windows 未安装 OCR 语言包。请在 设置 → 时间和语言 → 语言 中安装语言的基本键入/文字识别功能。' }
    $file = Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync([IO.Path]::GetFullPath($InputPath))) ([Windows.Storage.StorageFile])
    $builder = New-Object Text.StringBuilder
    if ([IO.Path]::GetExtension($InputPath) -ieq '.pdf') {
        $pdf = Await ([Windows.Data.Pdf.PdfDocument]::LoadFromFileAsync($file)) ([Windows.Data.Pdf.PdfDocument])
        for ($i = 0; $i -lt $pdf.PageCount; $i++) {
            $page = $pdf.GetPage([uint32]$i)
            $stream = New-Object Windows.Storage.Streams.InMemoryRandomAccessStream
            try {
                $options = New-Object Windows.Data.Pdf.PdfPageRenderOptions
                $options.DestinationWidth = [uint32][Math]::Min(2400,[Math]::Max(1,$page.Size.Width * 2))
                AwaitAction ($page.RenderToStreamAsync($stream,$options))
                $stream.Seek(0)
                $null = $builder.AppendLine(('【第 {0} 页】' -f ($i+1))).AppendLine((ReadImage $stream)).AppendLine()
            } finally { $stream.Dispose(); $page.Dispose() }
        }
    } else {
        $stream = Await ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
        try { $null = $builder.Append((ReadImage $stream)) } finally { $stream.Dispose() }
    }
    [IO.File]::WriteAllText($OutputPath,$builder.ToString(),(New-Object Text.UTF8Encoding($false)))
    exit 0
} catch {
    [IO.File]::WriteAllText($OutputPath,('OCR 失败：' + $_.Exception.Message),(New-Object Text.UTF8Encoding($false)))
    exit 1
}
