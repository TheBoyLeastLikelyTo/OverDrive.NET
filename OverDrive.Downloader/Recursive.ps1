# Main script execution
$parentDirectory = $args[0]

# Check if the parent directory exists
if (-not (Test-Path $parentDirectory -PathType Container)) {
    Write-Host "[ERROR] The specified directory does not exist."
    exit
}

# Get a list of .ODM files in all subdirectories of the parent directory
$ODMFiles = Get-ChildItem -Path $parentDirectory -Recurse -Filter *.ODM -File

# Loop through each .ODM file and execute the script with the file's path
foreach ($ODM in $ODMFiles) {
    $Path = $ODM.FullName
    Write-Host "[INFO] Downloading .ODM file: $Path"
    .\OverDrive.Downloader $Path
}
