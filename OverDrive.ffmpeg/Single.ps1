function ValidateInputDirectory($input_folder) {
    if (-not $input_folder -or -not (Test-Path -Path $input_folder -PathType Container) -or -not (Test-Path -Path "$input_folder\*.mp3")) {
        Write-Host "[ERROR] Invalid input folder or no MP3 files found." -ForegroundColor Red
        exit
    }
}

function ParseDirectoryName($input_folder) {
    $splitValues = (Split-Path -Path $input_folder -Leaf) -split ' - '
    if ($splitValues.Count -ne 2) {
        Write-Host "[ERROR] Invalid directory name format." -ForegroundColor Red
        exit
    }
    return $splitValues[0], $splitValues[1]
}

# Main script execution
$input_folder = $args[0]

# Ensure input directory exists, and contains MP3 files
ValidateInputDirectory $input_folder 

# Get the title and author from the directory name
$author, $title = ParseDirectoryName $input_folder 

Write-Host "[INFO] Current Book: $title by $author"

# Set temporary MKA path
$output_file = Join-Path -Path $input_folder -ChildPath "combined.mka"

# Create temporary list of MP3 files
$temp_list = "temp.txt"
Get-ChildItem -Path "$input_folder\*.mp3" | ForEach-Object { "file '$($_.FullName -replace "'", "'\''")'" } | Set-Content -Path $temp_list

# Prepare cover and chapters file paths
$cover_file = Join-Path -Path $input_folder -ChildPath "cover.jpg"
$chapters_file = Join-Path -Path $input_folder -ChildPath "chapters.txt"

if (Test-Path -Path $output_file) { # If MKA already exists, delete it
    Remove-Item $output_file
}

# Prepare the constant FFMPEG commands
$commandMeta = "-metadata title=`"$title`" -metadata artist=`"$author`" -metadata album=`"$title`""
$commandCover = " -attach `"$cover_file`" -metadata:s:t mimetype=image/jpeg -metadata:s:t filename=cover.jpg"
$commandChapters = "-i `"$chapters_file`" "

$midCommand = switch ($true) {
    { (-not (Test-Path -Path $cover_file)) -and (-not (Test-Path -Path $chapters_file)) } { # No chap or cover
        Write-Host "[INFO] No Chapters or Cover found." -ForegroundColor Yellow
        $commandMeta
    }
    { (Test-Path -Path $cover_file) -and (-not (Test-Path -Path $chapters_file)) } { # Cover no chap
        Write-Host "[INFO] Cover found, no Chapters found." -ForegroundColor Yellow
        $commandMeta + $commandCover
    }
    { (Test-Path -Path $chapters_file) -and (-not (Test-Path -Path $cover_file)) } { # Chap no cover
        Write-Host "[INFO] Chapters found, no Cover found." -ForegroundColor Yellow
        $commandChapters + $commandMeta
    }
    { (Test-Path -Path $chapters_file) -and (Test-Path -Path $cover_file) } { # Cover and chap
        Write-Host "[INFO] Cover and Chapters found." -ForegroundColor Cyan
        $commandChapters + $commandMeta + $commandCover
    }
    default { "" }
}

# Prepare the FFMPEG command
$fullCommand = ".\ffmpeg -f concat -safe 0 -i `"$temp_list`" $midCommand -c copy `"$output_file`" -hide_banner -loglevel error"

Write-Host "[INFO] Combining..." -ForegroundColor Cyan

# Run FFMPEG command
Invoke-Expression $fullCommand

# Remove temporary MP3 list
Remove-Item $temp_list

if (Test-Path -Path $output_file) { # Check that the MKA exists
    $new_output = "$input_folder\$title.mka" # It will be named from the book title
    Copy-Item -Path $output_file -Destination $new_output # Copy it to its new name
	Remove-Item -Path $output_file # Delete the former one
    Write-Host "[INFO] Successfully combined '$title' by '$author'" -ForegroundColor Green
} else {
    Write-Host "[ERROR] Failure combining '$title' by '$author'" -ForegroundColor Red
}

exit
