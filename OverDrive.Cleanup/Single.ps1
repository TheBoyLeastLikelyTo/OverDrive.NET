# Main script execution
$parentDirectory = $args[0]

# Check if the parent directory exists
if (-not (Test-Path $parentDirectory -PathType Container)) {
    Write-Host "[ERROR] The specified directory does not exist." -ForegroundColor Red
    exit
}

# Check for *.mka files
$mkaFiles = Get-ChildItem -Path $parentDirectory -Filter "*.mka"
if (-not $mkaFiles) {
    Write-Host "[ERROR] No *.mka files found in the directory. Exiting." -ForegroundColor Red
    exit
}

# Delete cover.jpg file
$coverFile = Join-Path -Path $parentDirectory -ChildPath "cover.jpg"
if (Test-Path $coverFile) {
    Remove-Item $coverFile -Force
    Write-Host "[INFO] Deleted cover.jpg" -ForegroundColor Green
}

# Delete *.license files
$licenseFiles = Get-ChildItem -Path $parentDirectory -Filter "*.license"
foreach ($license in $licenseFiles) {
    $Path = $license.FullName
    Write-Host "[INFO] Deleted license file: $Path" -ForegroundColor Green
    Remove-Item $Path -Force
}

# Delete *.metadata files
$metadataFiles = Get-ChildItem -Path $parentDirectory -Filter "*.metadata"
foreach ($metadata in $metadataFiles) {
    $Path = $metadata.FullName
    Write-Host "[INFO] Deleted metadata file: $Path" -ForegroundColor Green
    Remove-Item $Path -Force
}

# Delete *.mp3 files
$mp3Files = Get-ChildItem -Path $parentDirectory -Filter "*.mp3"
foreach ($mp3 in $mp3Files) {
    $Path = $mp3.FullName
    Write-Host "[INFO] Deleted MP3 file: $Path" -ForegroundColor Green
    Remove-Item $Path -Force
}

# Delete files with "chapter" in the name
$chapterFiles = Get-ChildItem -Path $parentDirectory | Where-Object { $_.Name -like '*chapter*' }
foreach ($chapter in $chapterFiles) {
    $Path = $chapter.FullName
    Write-Host "[INFO] Deleted chapter file: $Path" -ForegroundColor Green
    Remove-Item $Path -Force
}
