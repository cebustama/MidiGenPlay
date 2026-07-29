@echo off
setlocal
cd /d "%~dp0"

rem ============================================================
rem  mgp-export.bat  --  MidiGenPlay tagged mirror exporter
rem
rem  Run from the PACKAGE ROOT (the folder containing
rem  Documentation~, Runtime, Editor).
rem
rem  Usage:
rem     mgp-export.bat                       -> menu, tag = MGP-yyyymmdd
rem     mgp-export.bat sets\session-b.txt    -> that set, tag = MGP-yyyymmdd
rem     mgp-export.bat sets\session-b.txt MGP-20260728-FASEB
rem
rem  Every exported file is copied FLAT and renamed
rem     <TAG>_<originalname>
rem  and a <TAG>_MANIFEST.md is written with source path,
rem  last-write time, size and SHA256 of each file.
rem
rem  Result: <TAG>_export.zip next to this script.
rem
rem  In the consuming PK, deleting the obsolete mirror is then
rem  "delete everything whose prefix is not the current tag".
rem ============================================================

set "ROOT=%CD%"
set "LIST_FILE=%~1"
set "TAG=%~2"

if not exist "%ROOT%\Documentation~" (
    echo WARNING: no "Documentation~" folder here.
    echo          Are you running this from the package root?
    echo.
)

if "%LIST_FILE%"=="" (
    echo.
    echo Choose export set:
    echo   1^) session-b   - minimal file set for the next authoring session
    echo   2^) pk-mirror   - full set to refresh the consumer PK
    echo   3^) custom      - use mgp-export_custom.txt
    echo.
    choice /c 123 /n /m "Select 1, 2 or 3: "
    if errorlevel 3 set "LIST_FILE=%~dp0mgp-export_custom.txt"
    if errorlevel 3 goto :haveList
    if errorlevel 2 set "LIST_FILE=%~dp0mgp-export_pk-mirror.txt"
    if errorlevel 2 goto :haveList
    set "LIST_FILE=%~dp0mgp-export_session-b.txt"
)
:haveList

if not exist "%LIST_FILE%" (
    echo ERROR: list file not found: "%LIST_FILE%"
    pause
    exit /b 1
)

if "%TAG%"=="" (
    for /f %%d in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd"') do set "TAG=MGP-%%d"
)

set "ZIP_FILE=%~dp0%TAG%_export.zip"
set "PS1_FILE=%TEMP%\mgp_export_%RANDOM%_%RANDOM%.ps1"

echo.
echo Root : %ROOT%
echo List : %LIST_FILE%
echo Tag  : %TAG%
echo.

set "ROOT_ENV=%ROOT%"
set "LIST_ENV=%LIST_FILE%"
set "ZIP_ENV=%ZIP_FILE%"
set "TAG_ENV=%TAG%"

rem ---- generate the worker script (pipe-free on purpose: cmd echo hates pipes)
> "%PS1_FILE%" echo $ErrorActionPreference = 'Stop'
>> "%PS1_FILE%" echo $root = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $env:ROOT_ENV).Path)
>> "%PS1_FILE%" echo $sep = [System.IO.Path]::DirectorySeparatorChar.ToString()
>> "%PS1_FILE%" echo $rootPrefix = $root
>> "%PS1_FILE%" echo if (-not $rootPrefix.EndsWith($sep)) { $rootPrefix += $sep }
>> "%PS1_FILE%" echo $listFile = (Resolve-Path -LiteralPath $env:LIST_ENV).Path
>> "%PS1_FILE%" echo $zipFile = [System.IO.Path]::GetFullPath($env:ZIP_ENV)
>> "%PS1_FILE%" echo $tag = $env:TAG_ENV
>> "%PS1_FILE%" echo $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ('mgp_export_' + [System.Guid]::NewGuid().ToString('N'))
>> "%PS1_FILE%" echo [void](New-Item -ItemType Directory -Path $tempDir)
>> "%PS1_FILE%" echo $rows = New-Object System.Collections.ArrayList
>> "%PS1_FILE%" echo $missing = New-Object System.Collections.ArrayList
>> "%PS1_FILE%" echo $seen = @{}
>> "%PS1_FILE%" echo try {
>> "%PS1_FILE%" echo     if (Test-Path -LiteralPath $zipFile) { Remove-Item -LiteralPath $zipFile -Force }
>> "%PS1_FILE%" echo     $lines = Get-Content -LiteralPath $listFile
>> "%PS1_FILE%" echo     foreach ($raw in $lines) {
>> "%PS1_FILE%" echo         if ($null -eq $raw) { continue }
>> "%PS1_FILE%" echo         $entry = $raw.Trim()
>> "%PS1_FILE%" echo         if ($entry.Length -eq 0) { continue }
>> "%PS1_FILE%" echo         if ($entry.StartsWith('#')) { continue }
>> "%PS1_FILE%" echo         $found = @()
>> "%PS1_FILE%" echo         if ([System.IO.Path]::IsPathRooted($entry)) {
>> "%PS1_FILE%" echo             if (Test-Path -LiteralPath $entry -PathType Leaf) { $found = @(Get-Item -LiteralPath $entry) }
>> "%PS1_FILE%" echo         } elseif ($entry.Contains('\') -or $entry.Contains('/')) {
>> "%PS1_FILE%" echo             $cand = Join-Path $root ($entry.Replace('/','\'))
>> "%PS1_FILE%" echo             if (Test-Path -LiteralPath $cand -PathType Leaf) { $found = @(Get-Item -LiteralPath $cand) }
>> "%PS1_FILE%" echo         } else {
>> "%PS1_FILE%" echo             $found = @(Get-ChildItem -LiteralPath $root -Recurse -File -Filter $entry -ErrorAction SilentlyContinue)
>> "%PS1_FILE%" echo         }
>> "%PS1_FILE%" echo         if ($found.Count -eq 0) { [void]$missing.Add($entry); Write-Host ('  MISSING  ' + $entry); continue }
>> "%PS1_FILE%" echo         foreach ($f in $found) {
>> "%PS1_FILE%" echo             if ($null -eq $f) { continue }
>> "%PS1_FILE%" echo             $full = [System.IO.Path]::GetFullPath($f.FullName)
>> "%PS1_FILE%" echo             if ($seen.ContainsKey($full)) { continue }
>> "%PS1_FILE%" echo             $seen[$full] = $true
>> "%PS1_FILE%" echo             $rel = $f.Name
>> "%PS1_FILE%" echo             if ($full.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) { $rel = $full.Substring($rootPrefix.Length) }
>> "%PS1_FILE%" echo             $flat = $tag + '_' + $f.Name
>> "%PS1_FILE%" echo             $dest = Join-Path $tempDir $flat
>> "%PS1_FILE%" echo             $n = 2
>> "%PS1_FILE%" echo             while (Test-Path -LiteralPath $dest) {
>> "%PS1_FILE%" echo                 $flat = ('{0}_{1} ({2}){3}' -f $tag, [System.IO.Path]::GetFileNameWithoutExtension($f.Name), $n, $f.Extension)
>> "%PS1_FILE%" echo                 $dest = Join-Path $tempDir $flat
>> "%PS1_FILE%" echo                 $n++
>> "%PS1_FILE%" echo             }
>> "%PS1_FILE%" echo             Copy-Item -LiteralPath $full -Destination $dest -Force
>> "%PS1_FILE%" echo             $hash = (Get-FileHash -LiteralPath $dest -Algorithm SHA256).Hash
>> "%PS1_FILE%" echo             $stamp = $f.LastWriteTime.ToString('yyyy-MM-dd HH:mm')
>> "%PS1_FILE%" echo             [void]$rows.Add(('^| {0} ^| `{1}` ^| {2} ^| {3} ^| {4} ^|' -f $flat, $rel, $stamp, $f.Length, $hash.Substring(0,12)))
>> "%PS1_FILE%" echo             Write-Host ('  ok       ' + $rel)
>> "%PS1_FILE%" echo         }
>> "%PS1_FILE%" echo     }
>> "%PS1_FILE%" echo     if ($rows.Count -eq 0) { throw 'No files matched the list; nothing exported.' }
>> "%PS1_FILE%" echo     $md = New-Object System.Collections.ArrayList
>> "%PS1_FILE%" echo     [void]$md.Add('# ' + $tag + ' - MidiGenPlay mirror manifest')
>> "%PS1_FILE%" echo     [void]$md.Add('')
>> "%PS1_FILE%" echo     [void]$md.Add('Exported: ' + (Get-Date -Format 'yyyy-MM-dd HH:mm'))
>> "%PS1_FILE%" echo     [void]$md.Add('Source root: `' + $root + '`')
>> "%PS1_FILE%" echo     [void]$md.Add('Set: `' + [System.IO.Path]::GetFileName($listFile) + '`')
>> "%PS1_FILE%" echo     [void]$md.Add('')
>> "%PS1_FILE%" echo     [void]$md.Add('READ-ONLY MIRROR. Authority lives in the MidiGenPlay package.')
>> "%PS1_FILE%" echo     [void]$md.Add('Anything in the consuming PK NOT prefixed ' + $tag + ' is older than this export.')
>> "%PS1_FILE%" echo     [void]$md.Add('')
>> "%PS1_FILE%" echo     [void]$md.Add('^| Exported name ^| Source path ^| Last write ^| Bytes ^| SHA256 ^|')
>> "%PS1_FILE%" echo     [void]$md.Add('^|---^|---^|---^|---^|---^|')
>> "%PS1_FILE%" echo     foreach ($r in $rows) { [void]$md.Add($r) }
>> "%PS1_FILE%" echo     if ($missing.Count -gt 0) {
>> "%PS1_FILE%" echo         [void]$md.Add('')
>> "%PS1_FILE%" echo         [void]$md.Add('## Not found (check the set list)')
>> "%PS1_FILE%" echo         foreach ($m in $missing) { [void]$md.Add('- `' + $m + '`') }
>> "%PS1_FILE%" echo     }
>> "%PS1_FILE%" echo     Set-Content -LiteralPath (Join-Path $tempDir ($tag + '_MANIFEST.md')) -Value $md -Encoding UTF8
>> "%PS1_FILE%" echo     Compress-Archive -Path (Join-Path $tempDir '*') -DestinationPath $zipFile -Force
>> "%PS1_FILE%" echo     Write-Host ''
>> "%PS1_FILE%" echo     Write-Host ('Exported ' + $rows.Count + ' file(s); ' + $missing.Count + ' missing.')
>> "%PS1_FILE%" echo }
>> "%PS1_FILE%" echo finally {
>> "%PS1_FILE%" echo     if (Test-Path -LiteralPath $tempDir) { Remove-Item -LiteralPath $tempDir -Recurse -Force }
>> "%PS1_FILE%" echo }

powershell -NoProfile -ExecutionPolicy Bypass -File "%PS1_FILE%"
set "ERR=%ERRORLEVEL%"
del "%PS1_FILE%" >nul 2>&1

if not "%ERR%"=="0" (
    echo Export failed.
    pause
    exit /b %ERR%
)

echo.
echo Done. Zip: "%ZIP_FILE%"
pause
