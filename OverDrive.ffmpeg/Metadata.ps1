# Get the input directory from the command line argument
$inputPath = $args[0]

# Check if the input directory is provided
if (-not $inputPath) {
    Write-Host "Please provide an input directory path."
    exit
}

# Get the last part of the path (directory name)
$inputDirectory = Split-Path -Path $inputPath -Leaf

# Split the directory name by the dash "-"
$splitValues = $inputDirectory -split ' - '

# Check if the directory name was split into two parts
if ($splitValues.Count -eq 2) {
    $author = $splitValues[0]
    $title = $splitValues[1]

    Write-Host "Author: $author"
    Write-Host "Title: $title"
} else {
    Write-Host "Invalid directory name format. Please make sure it follows the 'Author - Title' format."
}
