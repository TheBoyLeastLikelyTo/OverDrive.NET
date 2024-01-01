# Main script execution
$parentDirectory = $args[0]

# Check if the parent directory exists
if (-not (Test-Path $parentDirectory -PathType Container)) {
    Write-Host "[ERROR] The specified directory does not exist." -ForegroundColor Red
    exit
}

# Get a list of subdirectories in the parent directory
$Books = Get-ChildItem -Path $parentDirectory -Directory

# Loop through each subdirectory and execute the script with the subdirectory path
foreach ($Book in $Books) {
    $Path = $Books.FullName
    Write-Host "[INFO] Chapterizing book in subdirectory: $Path" -ForegroundColor Cyan
    .\OverDrive.Chapters $Path
}
