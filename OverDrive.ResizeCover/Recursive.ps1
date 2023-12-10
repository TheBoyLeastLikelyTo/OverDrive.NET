# Main script execution
$parentDirectory = $args[0]

# Check if the parent directory exists
if (-not (Test-Path $parentDirectory -PathType Container)) {
    Write-Host "[ERROR] The specified directory does not exist."
    exit
}

# Get a list of .ODM files in all subdirectories of the parent directory
$CoverFiles = Get-ChildItem -Path $parentDirectory -Recurse -Filter cover.jpg -File

# Loop through each .ODM file and execute the script with the file's path
foreach ($Cover in $CoverFiles) {
    $Path = $Cover.FullName
    Write-Host "[INFO] Squaring Cover file: $Path"
    .\OverDrive.ResizeCover $Path
}
