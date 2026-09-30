Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function New-UnityTestOutput {
    param([string]$OutputRoot, [guid]$InvocationId = [guid]::NewGuid())
    # 出力 root は共有しても、GUID 子ディレクトリは invocation が単独で所有する。
    $root = [IO.Path]::GetFullPath($OutputRoot)
    [void][IO.Directory]::CreateDirectory($root)
    $directory = [IO.Path]::Combine($root, $InvocationId.ToString())
    # 既存 run を再利用すると、古い XML が今回の結果に混入する。
    if ([IO.Directory]::Exists($directory) -or [IO.File]::Exists($directory)) { throw "出力先が既に存在します: $directory" }
    New-Item -ItemType Directory -Path $directory -ErrorAction Stop | Out-Null
    return [pscustomobject]@{
        InvocationId=$InvocationId.ToString(); Directory=$directory
        XmlPath=[IO.Path]::Combine($directory, 'results.xml'); LogPath=[IO.Path]::Combine($directory, 'unity.log')
        StepPath=[IO.Path]::Combine($directory, 'step.json')
    }
}

function Read-UnityTestOutput {
    param([psobject]$Output)
    $failure = [Collections.Generic.List[string]]::new()
    $logs = [Collections.Generic.List[string]]::new()
    $hashes = [Collections.Generic.List[string]]::new()
    $xmlText = $null
    $logText = $null
    # 存在する生ファイルだけを XML→log の順で固定する。欠落は policy にも伝えて failed にする。
    foreach ($entry in @(@('results.xml', $Output.XmlPath), @('unity.log', $Output.LogPath))) {
        $name = $entry[0]
        $path = $entry[1]
        if (-not [IO.File]::Exists($path)) { $failure.Add("$name がありません"); continue }
        try {
            $content = [IO.File]::ReadAllText($path)
            $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($path)))
            if ($name -eq 'results.xml') { $xmlText = $content } else { $logText = $content }
            $logs.Add($name)
            $hashes.Add($hash)
        } catch { $failure.Add("$name を読み取れません: $($_.Exception.Message)") }
    }
    # H1 と同じ算法: 各ファイルの大文字 SHA256 を | で結び、その UTF-8 を小文字 SHA256 にする。
    # 0 件でも空文字列の hash を持つが、成功した証拠という意味はない。
    $joined = [string]::Join('|', $hashes)
    $logHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($joined))).ToLowerInvariant()
    return [pscustomobject]@{ XmlText=$xmlText; LogText=$logText; Failure=@($failure.ToArray()); Logs=@($logs.ToArray()); LogHash=$logHash }
}

function Save-UnityTestStep {
    param([psobject]$Output, [object]$Step)
    $json = ConvertTo-Json -InputObject $Step -Depth 16
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($json + "`n")
    # step 自体を logHash に含めず自己参照を避ける。CreateNew で二度目の確定も拒否する。
    $stream = [IO.FileStream]::new($Output.StepPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $stream.Write($bytes, 0, $bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
    return $Output.StepPath
}

Export-ModuleMember -Function New-UnityTestOutput, Read-UnityTestOutput, Save-UnityTestStep
