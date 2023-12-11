# Main script execution
$parentDirectory = $args[0]

# Check if the parent directory exists
if (-not (Test-Path $parentDirectory -PathType Container)) {
    Write-Host "[ERROR] The specified directory does not exist."
    exit
}

# Get a list of subdirectories in the parent directory
$Books = Get-ChildItem -Path $parentDirectory -Directory

# Loop through each subdirectory and execute the script with the subdirectory path
foreach ($Book in $Books) {
    $Path = $Book.FullName
    Write-Host "[INFO] Cleaning book in subdirectory: $Path"
    .\Single.ps1 $Path
}

# Delete *.odm files
$odmFiles = Get-ChildItem -Path $parentDirectory -Filter "*.odm"
foreach ($odm in $odmFiles) {
    $Path = $odm.FullName
    Write-Host "[INFO] Deleted ODM file: $Path" -ForegroundColor Green
    Remove-Item $Path -Force
}