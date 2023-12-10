# Get the input directory from the command line argument
$input_folder = $args[0]

# Check that there is a provided path
if (-not $input_folder) {
    Write-Host "[ERROR] Please provide the input directory." -ForegroundColor Red
    exit
}

# Check that the path is an existing directory
if (-not (Test-Path -Path $input_folder -PathType Container)) {
    Write-Host "[ERROR] Input folder does not exist." -ForegroundColor Red
    exit
}

# Check that the directory contains MP3 files
if (-not (Test-Path -Path "$input_folder\*.mp3")) {
    Write-Host "[ERROR] No MP3 files found in the input folder." -ForegroundColor Red
    exit
}


# Gets the author and title from the book folder name

$inputDirectory = Split-Path -Path $input_folder -Leaf # Get the last part of the path (directory name)
$splitValues = $inputDirectory -split ' - ' # Split the directory name by the dash "-"
$author = "" # Prepare author name string
$title = "" # Prepare book title/album name string

# Check if the directory name was split into two parts
if ($splitValues.Count -eq 2) {
    $author = $splitValues[0]
    $title = $splitValues[1]    
}
else {
    Write-Host "[ERROR] Invalid directory name format. Please make sure it follows the 'Author - Title' format."
    exit
}

Write-Host "Author: $author"
Write-Host "Title: $title"


# Comnbines MP3s to MKA

$output_file = "$input_folder\combined.mka" # Prepare the combined MP3 filepath
Write-Host "[MP3 PARTS] Using output file path: '$output_file'" -ForegroundColor Cyan
if (Test-Path -Path $output_file) { Remove-Item $output_file } # Delete existing combined MKA file

# Create a temporary list of all the MP3s for use with ffmpeg combination
$temp_list = "temp.txt"
Get-ChildItem -Path "$input_folder\*.mp3" | ForEach-Object { "file '$($_.FullName)'" } | Set-Content -Path $temp_list

Write-Host "[MP3 PARTS] Writing MP3 parts to new file, '$output_file'" -ForegroundColor Cyan
.\ffmpeg -f concat -safe 0 -i $temp_list -c copy $output_file -hide_banner -loglevel error

Remove-Item $temp_list

if (Test-Path -Path $output_file) { Write-Host "[MP3 PARTS] All MP3 files from '$input_folder' combined into '$output_file'." -ForegroundColor Green }
else { Write-Host "[ERROR] Error combining MP3 parts to '$output_file'" -ForegroundColor Red }


# Adds a chapters file to the MKA

$chapters_file = "$input_folder\chapters.txt" # Prepare the chapters FFMPEGMETADATA filepath
Write-Host "[CHAPTERS] Searching for chapters file: $chapters_file" -ForegroundColor Cyan
$output_file_with_chapters = "$input_folder\chaptered.mka" # Prepare the chaptered MKA filepath

if (Test-Path -Path $chapters_file) { # If there is a txt file with chapters,
    
    if (Test-Path -Path $output_file_with_chapters) { Remove-Item $output_file_with_chapters } # Delete existing chaptered MKA file
    
    Write-Host "[CHAPTERS] Chapters file found, writing chapters to new file, '$output_file_with_chapters'" -ForegroundColor Cyan
    .\ffmpeg -i $output_file -i $chapters_file -map_metadata 1 -codec copy $output_file_with_chapters -hide_banner -loglevel error

    if (Test-Path -Path $output_file_with_chapters) { # If the chaptered MKA was created successfully

        Remove-Item $output_file # Delete the previously created MKA
        Write-Host "[CHAPTERS] Chapters written to '$output_file_with_chapters' successfully, original file deleted." -ForegroundColor Green
        $output_file = $output_file_with_chapters # Update current MKA file
    }
    else { Write-Host "[ERROR] Error writing chaptered version of '$output_file'" -ForegroundColor Red } # If the chaptered MKA was not created successfully
}
else { Write-Host "[CHAPTERS] No chapters file found at $chapters_file" -ForegroundColor Red } # If no txt file with chapters exists


# Adds a cover art file to the MKA

$cover_file = "$input_folder\cover.jpg" # Prepare the cover filepath
Write-Host "[COVER] Searching for cover file: $cover_file" -ForegroundColor Cyan
$output_file_with_cover = "$input_folder\covered.mka" # Prepare the covered MKA filepath

if (Test-Path -Path $cover_file) { # If there is a cover file,
    
    if (Test-Path -Path $output_file_with_cover) { Remove-Item $output_file_with_cover } # Delete existing covered MKA file
    
    Write-Host "[COVER] Cover file found, writing cover to $output_file" -ForegroundColor Cyan
    .\ffmpeg -i $output_file -c copy -attach $cover_file -metadata:s:t mimetype=image/jpeg $output_file_with_cover -hide_banner -loglevel error

    if (Test-Path -Path $output_file_with_cover) { # If the covered MKA was created successfully
        
        Remove-Item $output_file # Delete the previously created MKA
        Write-Host "[COVER] Cover written to '$output_file_with_cover' successfully, original file deleted." -ForegroundColor Green
        $output_file = $output_file_with_cover # Update current MKA file
    }
    else { Write-Host "[ERROR] Error writing cover version of '$output_file'" -ForegroundColor Red } # If the covered MKA was not created successfully
}
else { Write-Host "[COVER] No cover file found at '$cover_file'" -ForegroundColor Red } # If no cover file exists

if (Test-Path -Path $output_file) { Write-Host "[INFO] '$output_file' written successfully!" -ForegroundColor Green } # A final MKA file exists
else { Write-Host "[ERROR] Error writing '$output_file'" -ForegroundColor Red } # No MKA file was created

exit