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
    $Path = $Book.FullName
    Write-Host "[INFO] Cleaning book in subdirectory: $Path" -ForegroundColor Cyan
    .\Single.ps1 $Path
}

# Get a list of .ODM files in the current directory
$OdmFiles = Get-ChildItem -Path $parentDirectory -Filter "*.odm"

# Loop through each .ODM file and execute the script with the file's path
foreach ($odm in $OdmFiles) {
    $Path = $odm.FullName
    Write-Host "[INFO] Deleted ODM file: $Path" -ForegroundColor Green
    Remove-Item $Path -Force
}