param(
    [string]$ZipName = "TKSM.0.1.9.zip"
)

$root = Get-Location
$zipPath = Join-Path $root $ZipName

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Write-Host "Zipping solution from $root ..."

$items = Get-ChildItem -Recurse -Force |
    Where-Object {
        -not $_.PSIsContainer -and
        $_.FullName -notmatch '\\(bin|obj|\.git|\.vs)\\' -and
        $_.Name -notmatch '\.zip$' -and
        -not $_.Attributes.ToString().Contains("Hidden")
    }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipStream = [System.IO.Compression.ZipFile]::Open($zipPath, 'Create')

foreach ($f in $items) {
    $rel = $f.FullName.Substring($root.Path.Length + 1)
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zipStream, $f.FullName, $rel)
}

$zipStream.Dispose()

Write-Host "Created $ZipName"
