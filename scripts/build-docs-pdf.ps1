# Converts /docs/*.md into a single /docs/PixelAndBit_Documentation.pdf
# using a local Chrome/Edge install in headless "print to PDF" mode.
# No external packages required; the markdown renderer is a compact in-script converter.

param(
    [string]$DocsDir = (Join-Path $PSScriptRoot "..\docs"),
    [string]$Output  = (Join-Path $PSScriptRoot "..\docs\PixelAndBit_Documentation.pdf")
)

$ErrorActionPreference = 'Stop'

# --- 0. Find a headless-capable browser ------------------------------------
$candidates = @(
    "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
    "$env:ProgramFiles(x86)\Google\Chrome\Application\chrome.exe",
    "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles(x86)\Microsoft\Edge\Application\msedge.exe",
    "$env:LOCALAPPDATA\Microsoft\Edge\Application\msedge.exe"
)
$browser = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $browser) { throw "No Chrome/Edge found. Install Chrome or run pandoc instead." }

# --- 1. Collect docs in the intended reading order -------------------------
$order = @(
    'PixelAndBit_Documentation.md',
    '01_Overview.md',
    '02_Backend.md',
    '03_Frontend.md',
    '04_Database_Auth_Config.md',
    '05_Deployment_and_Study.md'
)
$parts = @()
foreach ($name in $order) {
    $path = Join-Path $DocsDir $name
    if (Test-Path $path) { $parts += [pscustomobject]@{ Name=$name; Text=(Get-Content $path -Raw -Encoding UTF8) } }
}
if ($parts.Count -eq 0) { throw "No markdown files found under $DocsDir" }

# --- 2. Compact Markdown -> HTML (deliberately simple; not a full CommonMark impl) ---
function Html-Escape([string]$s) {
    return ($s -replace '&','&amp;' -replace '<','&lt;' -replace '>','&gt;')
}

function Convert-MdToHtml([string]$md) {
    $md = $md -replace "`r`n","`n"

    # Stash fenced code blocks
    $codeBlocks = New-Object System.Collections.Generic.List[string]
    $md = [regex]::Replace($md, '```([a-zA-Z0-9_:+\-\.]*)\n([\s\S]*?)```', {
        param($m)
        $lang = $m.Groups[1].Value
        $code = Html-Escape($m.Groups[2].Value)
        $cls = if ($lang) { " class=`"lang-$lang`"" } else { "" }
        $html = "<pre><code$cls>$code</code></pre>"
        $codeBlocks.Add($html) | Out-Null
        return "{{CODEBLOCK_$($codeBlocks.Count - 1)}}"
    })

    # Stash inline code
    $inlineCode = New-Object System.Collections.Generic.List[string]
    $md = [regex]::Replace($md, '`([^`\n]+)`', {
        param($m)
        $c = Html-Escape($m.Groups[1].Value)
        $inlineCode.Add("<code>$c</code>") | Out-Null
        return "{{INLINECODE_$($inlineCode.Count - 1)}}"
    })

    # Escape remaining HTML
    $md = Html-Escape($md)

    # Tables (GFM pipe tables)
    $md = [regex]::Replace($md, '(^|\n)((?:\|[^\n]*\|\n){1,})(\|[\s\-:| ]+\|\n)((?:\|[^\n]*\|(?:\n|$))+)', {
        param($m)
        $lead    = $m.Groups[1].Value
        $header  = $m.Groups[2].Value
        $body    = $m.Groups[4].Value
        function RowCells([string]$row) {
            $r = $row.Trim()
            if ($r.StartsWith('|')) { $r = $r.Substring(1) }
            if ($r.EndsWith('|'))   { $r = $r.Substring(0, $r.Length - 1) }
            return ($r -split '\|') | ForEach-Object { $_.Trim() }
        }
        $headerRows = $header.TrimEnd("`n") -split "`n" | Where-Object { $_ }
        $bodyRows   = $body.TrimEnd("`n")   -split "`n" | Where-Object { $_ }
        $sb = [System.Text.StringBuilder]::new()
        [void]$sb.Append("$lead<table><thead>")
        foreach ($hr in $headerRows) {
            [void]$sb.Append('<tr>')
            foreach ($c in (RowCells $hr)) { [void]$sb.Append("<th>$c</th>") }
            [void]$sb.Append('</tr>')
        }
        [void]$sb.Append('</thead><tbody>')
        foreach ($br in $bodyRows) {
            [void]$sb.Append('<tr>')
            foreach ($c in (RowCells $br)) { [void]$sb.Append("<td>$c</td>") }
            [void]$sb.Append('</tr>')
        }
        [void]$sb.Append('</tbody></table>')
        return $sb.ToString()
    }, [System.Text.RegularExpressions.RegexOptions]::Multiline)

    # Headings (# through ######) -- longest prefix first
    for ($h=6; $h -ge 1; $h--) {
        $hashes = '#' * $h
        $md = [regex]::Replace($md, "(?m)^$hashes\s+(.+?)\s*$", "<h$h>`$1</h$h>")
    }

    # Horizontal rule
    $md = [regex]::Replace($md, "(?m)^---+\s*$", "<hr/>")

    # Blockquotes (single level, merged)
    $md = [regex]::Replace($md, "(?m)^&gt;\s?(.*)$", "<blockquote>`$1</blockquote>")
    $md = $md -replace '</blockquote>\s*<blockquote>', "<br/>"

    # Simple lists (flat; wraps contiguous list runs).
    # Treats "- x" and "* x" as <ul>; "1. x" as <ol>.
    $resultLines = New-Object System.Collections.Generic.List[string]
    $open = $null   # 'ul' or 'ol' or $null
    foreach ($line in ($md -split "`n")) {
        $isUl = [regex]::IsMatch($line, '^\s*[-*]\s+')
        $isOl = [regex]::IsMatch($line, '^\s*\d+\.\s+')
        if ($isUl -or $isOl) {
            $want = $(if ($isOl) { 'ol' } else { 'ul' })
            if ($open -ne $want) {
                if ($open) { [void]$resultLines.Add("</$open>") }
                [void]$resultLines.Add("<$want>")
                $open = $want
            }
            $content = $line -replace '^\s*(?:[-*]|\d+\.)\s+',''
            [void]$resultLines.Add("<li>$content</li>")
        } else {
            if ($open) { [void]$resultLines.Add("</$open>"); $open = $null }
            [void]$resultLines.Add($line)
        }
    }
    if ($open) { [void]$resultLines.Add("</$open>") }
    $md = ($resultLines -join "`n")

    # Bold / italic / links (applied after lists so leading "*" doesn't get italicised)
    $md = [regex]::Replace($md, '\*\*([^\*\n]+)\*\*', '<strong>$1</strong>')
    $md = [regex]::Replace($md, '(^|[^\*])\*([^\*\n]+)\*([^\*]|$)', '$1<em>$2</em>$3')
    $md = [regex]::Replace($md, '\[([^\]]+)\]\(([^)]+)\)', '<a href="$2">$1</a>')

    # Paragraph wrap the remaining loose text between blank lines
    $out = New-Object System.Text.StringBuilder
    foreach ($chunk in ($md -split "`n{2,}")) {
        $t = $chunk.Trim()
        if (-not $t) { continue }
        if ($t -match '^<(h\d|ul|ol|li|pre|blockquote|table|hr|p|div|header|footer|section)') {
            [void]$out.Append($t); [void]$out.Append("`n")
        } elseif ($t -match '^\{\{CODEBLOCK_') {
            [void]$out.Append($t); [void]$out.Append("`n")
        } else {
            [void]$out.Append("<p>")
            [void]$out.Append(($t -replace "`n", "<br/>"))
            [void]$out.Append("</p>`n")
        }
    }
    $md = $out.ToString()

    # Restore inline code
    $md = [regex]::Replace($md, '\{\{INLINECODE_(\d+)\}\}', {
        param($m); return $inlineCode[[int]$m.Groups[1].Value]
    })
    # Restore code blocks
    $md = [regex]::Replace($md, '\{\{CODEBLOCK_(\d+)\}\}', {
        param($m); return $codeBlocks[[int]$m.Groups[1].Value]
    })

    return $md
}

Write-Host "Converting markdown..."
$body = New-Object System.Text.StringBuilder
foreach ($p in $parts) {
    Write-Host "  + $($p.Name)"
    [void]$body.Append("<section class='doc-file'>")
    [void]$body.Append((Convert-MdToHtml $p.Text))
    [void]$body.Append("</section>")
}

# --- 3. Wrap in printable HTML ---------------------------------------------
$cssLines = @(
    "@page { size: A4; margin: 20mm 16mm 22mm 16mm; }",
    "* { box-sizing: border-box; }",
    "html, body { margin: 0; padding: 0; -webkit-print-color-adjust: exact; print-color-adjust: exact; }",
    "body { font-family: 'Segoe UI','Inter',Arial,sans-serif; color: #1f2328; line-height: 1.55; font-size: 11.5pt; }",
    ".doc-file { page-break-after: always; }",
    ".doc-file:last-child { page-break-after: auto; }",
    "h1,h2,h3,h4,h5,h6 { color: #0f1017; line-height: 1.25; margin: 1.4em 0 .55em; }",
    "h1 { font-size: 22pt; border-bottom: 2px solid #111418; padding-bottom: 6px; }",
    "h2 { font-size: 16pt; border-bottom: 1px solid #d0d7de; padding-bottom: 4px; }",
    "h3 { font-size: 13.5pt; }",
    "h4 { font-size: 12pt; }",
    "h5,h6 { font-size: 11pt; color: #4b5563; }",
    "p { margin: .6em 0; }",
    "ul,ol { margin: .4em 0 .9em 1.4em; padding: 0; }",
    "li { margin: .15em 0; }",
    "code { font-family: 'Consolas','Cascadia Code','Courier New',monospace; background: #f6f8fa; padding: 0 .25em; border-radius: 3px; font-size: 10pt; }",
    "pre { background: #f6f8fa; border: 1px solid #e5e7eb; border-radius: 6px; padding: .75em .9em; overflow-x: auto; white-space: pre-wrap; word-break: break-word; font-size: 10pt; line-height: 1.45; page-break-inside: avoid; }",
    "pre code { background: transparent; padding: 0; border-radius: 0; font-size: 10pt; }",
    "hr { border: 0; border-top: 1px solid #d0d7de; margin: 1.6em 0; }",
    "blockquote { border-left: 3px solid #d0d7de; color: #4b5563; margin: .8em 0; padding: .1em 0 .1em .8em; }",
    "table { border-collapse: collapse; width: 100%; margin: .9em 0; font-size: 10.5pt; page-break-inside: avoid; }",
    "th,td { border: 1px solid #d0d7de; padding: 6px 10px; text-align: left; vertical-align: top; }",
    "th { background: #f6f8fa; font-weight: 600; }",
    "a { color: #0366d6; text-decoration: none; word-break: break-word; }",
    "a:hover { text-decoration: underline; }",
    "strong { color: #111418; }",
    ".doc-header { text-align: center; padding: 2px 0 12px; border-bottom: 1px solid #e5e7eb; margin-bottom: 20px; }",
    ".doc-header .t { font-size: 18pt; font-weight: 700; color: #111418; letter-spacing: .02em; }",
    ".doc-header .s { font-size: 10.5pt; color: #6b7280; margin-top: 2px; }"
)
$css = [string]::Join("`n", $cssLines)

$nowStr  = [DateTime]::UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'")
$headerHtml = "<header class=`"doc-header`"><div class=`"t`">Pixel and Bit - Technical Documentation</div><div class=`"s`">Generated $nowStr</div></header>"
$html    = "<!DOCTYPE html><html lang=`"en`"><head><meta charset=`"utf-8`"/><title>Pixel and Bit - Documentation</title><style>$css</style></head><body>$headerHtml$($body.ToString())</body></html>"

$htmlPath = Join-Path $DocsDir "_pixelbit_doc_tmp.html"
[System.IO.File]::WriteAllText($htmlPath, $html, (New-Object System.Text.UTF8Encoding($false)))

# --- 4. Chrome headless print-to-PDF ---------------------------------------
$absHtml = (Resolve-Path $htmlPath).Path
$absPdf  = [System.IO.Path]::GetFullPath($Output)
# Chrome file:// URIs need URL-encoded spaces; % signs aren't in our paths so a simple replace is safe.
$fileUri = "file:///" + (($absHtml -replace '\\','/') -replace ' ','%20')

Write-Host "Browser: $browser"
Write-Host "HTML:    $absHtml"
Write-Host "PDF:     $absPdf"

if (Test-Path $absPdf) { Remove-Item $absPdf -Force }

$tmpProfile = Join-Path $env:TEMP ("pixelbit-chrome-" + [Guid]::NewGuid().ToString("N"))

$argList = @(
    '--headless=new',
    '--disable-gpu',
    '--no-sandbox',
    '--no-first-run',
    '--disable-extensions',
    '--disable-breakpad',
    '--no-pdf-header-footer',
    "--user-data-dir=$tmpProfile",
    "--print-to-pdf=$absPdf",
    $fileUri
)

Write-Host "Rendering PDF..."
Write-Host "  URI:         $fileUri"
Write-Host "  Output:      $absPdf"
Write-Host "  Profile dir: $tmpProfile"

$logOut = Join-Path $DocsDir "_pixelbit_doc_chrome_out.log"
$logErr = Join-Path $DocsDir "_pixelbit_doc_chrome_err.log"

# PowerShell 5.1's Start-Process -ArgumentList quotes each item, but Chrome parses
# "--print-to-pdf=<path with spaces>" as "--print-to-pdf=<path" <spaces-separated-targets>
# which triggers: "Multiple targets are not supported in headless mode".
# Workaround: pre-quote any argument that contains whitespace ourselves.
function QuoteArg([string]$a) {
    if ($a -match '\s') { return '"' + ($a -replace '"','\"') + '"' }
    return $a
}
function RunChrome([string[]]$rawArgs) {
    $quoted = ($rawArgs | ForEach-Object { QuoteArg $_ }) -join ' '
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName               = $browser
    $psi.Arguments              = $quoted
    $psi.UseShellExecute        = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError  = $true
    $psi.CreateNoWindow         = $true
    $proc = [System.Diagnostics.Process]::Start($psi)
    $stdOut = $proc.StandardOutput.ReadToEndAsync()
    $stdErr = $proc.StandardError.ReadToEndAsync()
    if (-not $proc.WaitForExit(60000)) { try { $proc.Kill() } catch {} }
    [System.IO.File]::WriteAllText($logOut, $stdOut.Result)
    [System.IO.File]::WriteAllText($logErr, $stdErr.Result)
    return $proc.ExitCode
}

$code = RunChrome $argList
Write-Host "  Exit code:   $code"
if (-not (Test-Path $absPdf)) {
    if (Test-Path $logErr) {
        $errTxt = Get-Content $logErr -Raw -ErrorAction SilentlyContinue
        if ($errTxt) { Write-Host "Chrome stderr:`n$errTxt" -ForegroundColor Yellow }
    }
    Write-Host "Retrying with legacy --headless flag..." -ForegroundColor Yellow
    $argList2 = @('--headless','--disable-gpu','--no-sandbox','--no-first-run','--disable-extensions','--disable-breakpad','--no-pdf-header-footer',"--user-data-dir=$tmpProfile","--print-to-pdf=$absPdf",$fileUri)
    $code2 = RunChrome $argList2
    Write-Host "  Exit code (retry): $code2"
}

Remove-Item $tmpProfile -Recurse -Force -ErrorAction SilentlyContinue

if (Test-Path $absPdf) {
    $size = (Get-Item $absPdf).Length
    Write-Host ("PDF created: {0} ({1:N0} bytes)" -f $absPdf, $size) -ForegroundColor Green
} else {
    throw "PDF was not created. Check the intermediate HTML at: $absHtml"
}

if (-not $env:PB_DOCS_DEBUG) {
    Remove-Item $htmlPath -ErrorAction SilentlyContinue
}
