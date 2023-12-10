# Function to check if the directory exists and contains MP3 files
function ValidateInputDirectory($input_folder) {
    if (-not $input_folder) {
        Write-Host "[ERROR] Please provide the input directory." -ForegroundColor Red
        exit
    }

    if (-not (Test-Path -Path $input_folder -PathType Container)) {
        Write-Host "[ERROR] Input folder does not exist: $input_folder" -ForegroundColor Red
        exit
    }

    if (-not (Test-Path -Path "$input_folder\*.mp3")) {
        Write-Host "[ERROR] No MP3 files found in the input folder." -ForegroundColor Red
        exit
    }
}

# Function to extract author and title from directory name
function ParseDirectoryName($input_folder) {
    $inputDirectory = Split-Path -Path $input_folder -Leaf
    $splitValues = $inputDirectory -split ' - ' 

    if ($splitValues.Count -eq 2) {
        return $splitValues[0], $splitValues[1]
    } else {
        Write-Host "[ERROR] Invalid directory name format. Please make sure it follows the 'Author - Title' format." -ForegroundColor Red
        exit
    }
}

# Main script execution
$input_folder = $args[0]

ValidateInputDirectory($input_folder)

$author, $title = ParseDirectoryName($input_folder)
Write-Host "Author: $author"
Write-Host "Title: $title"

$output_file = Join-Path -Path $input_folder -ChildPath "combined.mka"

$temp_list = "temp.txt"
Get-ChildItem -Path "$input_folder\*.mp3" | ForEach-Object { "file '$($_.FullName -replace "'", "'\''")'" } | Set-Content -Path $temp_list

$cover_file = Join-Path -Path $input_folder -ChildPath "cover.jpg"
$chapters_file = Join-Path -Path $input_folder -ChildPath "chapters.txt"

if (Test-Path -Path $output_file)
{
    Remove-Item $output_file
}

$fullCommand = ""
$commandStart = ".\ffmpeg -f concat -safe 0 -i `"$temp_list`" "
$commandChapters = "-i `"$chapters_file`" "
$commandMeta = "-metadata title=`"$title`" -metadata artist=`"$author`" -metadata album=`"$title`""
$commandCover = " -attach `"$cover_file`" -metadata:s:t mimetype=image/jpeg -metadata:s:t filename=cover.jpg"
$commandEnd = " -c copy `"$output_file`" -hide_banner -loglevel error"

if ((-not (Test-Path -Path $cover_file)) -and (-not (Test-Path -Path $chapters_file)))
{
    # No Cover or Chapters
    Write-Host "[INFO] No Chapters or Cover found." -ForegroundColor Cyan
    $fullCommand = $commandStart + $commandMeta + $commandEnd
}
elseif ((Test-Path -Path $cover_file) -and (-not (Test-Path -Path $chapters_file)))
{
    # Cover, but no Chapters
    Write-Host "[INFO] Cover found, no Chapters found."
    $fullCommand = $commandStart + $commandMeta + $commandCover + $commandEnd
}
elseif ((Test-Path -Path $chapters_file) -and (-not (Test-Path -Path $cover_file)))
{
    # Chapters, but no Cover
    Write-Host "[INFO] Chapters found, no Cover found."
    $fullCommand = $commandStart + $commandChapters + $commandMeta + $commandEnd
}
elseif ((Test-Path -Path $chapters_file) -and (Test-Path -Path $cover_file))
{
    # Cover and Chapters
    Write-Host "[INFO] Cover and Chapters found."
    $fullCommand = $commandStart + $commandChapters + $commandMeta + $commandCover + $commandEnd
}
else
{
    <# Action when all if and elseif conditions are false #>
}

#Write-Host $fullCommand

Write-Host "[INFO] Combining..." -ForegroundColor DarkYellow
Invoke-Expression $fullCommand

Remove-Item $temp_list

if (Test-Path -Path $output_file)
{
    $new_output = "$input_folder\$title.mka"
    Move-Item -Path $output_file -Destination $new_output
    Write-Host "[INFO] Successfully combined '$title' by '$author'" -ForegroundColor Green
}
else
{
    Write-Host "[ERROR] Failure combining '$title' by '$author'" -ForegroundColor Red
}

exit
