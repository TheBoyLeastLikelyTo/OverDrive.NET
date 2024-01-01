# Main script execution
$parentDirectory = $args[0]

# Check if the parent directory exists
if (-not (Test-Path $parentDirectory -PathType Container)) {
    Write-Host "[ERROR] The specified directory does not exist." -ForegroundColor Red
    exit
}

# Get a list of .ODM files in all subdirectories of the parent directory
$JpgFiles = Get-ChildItem -Path $parentDirectory -Recurse -Filter cover.jpg -File

# Loop through each .ODM file and execute the script with the file's path
foreach ($Cover in $JpgFiles) {
    $Path = $Cover.FullName
    Write-Host "[INFO] Squaring Cover file: $Path" -ForegroundColor Cyan
    .\OverDrive.ResizeCover $Path
}
