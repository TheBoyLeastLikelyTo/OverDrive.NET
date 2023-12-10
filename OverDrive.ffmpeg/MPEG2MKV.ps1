# Function to check if the directory exists and contains MP3 files
function ValidateInputDirectory($input_folder) {
    if (-not $input_folder) {
        Write-Host "[ERROR] Please provide the input directory." -ForegroundColor Red
        exit
    }

    if (-not (Test-Path -Path $input_folder -PathType Container)) {
        Write-Host "[ERROR] Input folder does not exist." -ForegroundColor Red
        exit
    }

    if (-not (Test-Path -Path "$input_folder\*.mp3")) {
        Write-Host "[ERROR] No MP3 files found in the input folder." -ForegroundColor Red
        exit
    }
}

# Function to extract author and title from directory name
function ParseDirectoryName($input_folder) {
    $inputDirectory = ParseFilePath $input_folder
    $splitValues = $inputDirectory -split ' - ' 

    if ($splitValues.Count -eq 2) {
        return $splitValues[0], $splitValues[1]
    } else {
        Write-Host "[ERROR] Invalid directory name format. Please make sure it follows the 'Author - Title' format."
        exit
    }
}

# Function to get the file name from a full path
function ParseFilePath($path) {
    return Split-Path -Path $path -Leaf 
}

# Function to combine MP3s into MKA
function Combine($input_folder) {
    
    $output_file = "$input_folder\combined.mka" # Prepare the combined MP3 filepath
    if (Test-Path -Path $output_file) { Remove-Item $output_file } # Delete existing combined MKA file

    # Create a temporary list of all the MP3s for use with ffmpeg combination
    $temp_list = "temp.txt"
    Get-ChildItem -Path "$input_folder\*.mp3" | ForEach-Object { "file '$($_.FullName)'" } | Set-Content -Path $temp_list

    Write-Host "[MP3 PARTS] Writing MP3 parts to new file, '$output_file'" -ForegroundColor Cyan
    .\ffmpeg -f concat -safe 0 -i $temp_list -c copy $output_file -hide_banner -loglevel error

    Remove-Item $temp_list

    if (Test-Path -Path $output_file) { Write-Host "[MP3 PARTS] MP3 files combined into '$output_file' successfully" -ForegroundColor Green }
    else { Write-Host "[ERROR] Error combining MP3 parts to '$output_file'" -ForegroundColor Red }

    return $output_file
}

# Function to add chapters to MKA
function Chapterize($output_file, $chapters_file) {
    
    Write-Host "[CHAPTERS] Searching for chapters file: $chapters_file" -ForegroundColor Cyan
    if (-not (Test-Path -Path $chapters_file)) {
        Write-Host "[CHAPTERS] No chapters.txt found, skipping"
        return $output_file
    }

    $output_file_with_chapters = "$input_folder\chaptered.mka" # Prepare the chaptered MKA filepath

    if (Test-Path -Path $output_file_with_chapters) { Remove-Item $output_file_with_chapters } # Delete existing chaptered MKA file

    Write-Host "[CHAPTERS] Chapters file found, writing chapters to new file, '$output_file_with_chapters'" -ForegroundColor Cyan
    .\ffmpeg -i $output_file -i $chapters_file -map_metadata 1 -codec copy $output_file_with_chapters -hide_banner -loglevel error

    if (Test-Path -Path $output_file_with_chapters) { # If the chaptered MKA was created successfully

        Remove-Item $output_file # Delete the previously created MKA
        Write-Host "[CHAPTERS] Chapters written to '$output_file_with_chapters' successfully" -ForegroundColor Green
        $output_file = $output_file_with_chapters # Update current MKA file
    }
    else { Write-Host "[ERROR] Error writing chaptered version of '$output_file'" -ForegroundColor Red } # If the chaptered MKA was not created successfully

    return $output_file
}

# Function to add cover art to MKA
function Cover($output_file, $cover_file) {

    Write-Host "[COVER] Searching for cover file: $cover_file" -ForegroundColor Cyan
    if (-not (Test-Path -Path $cover_file)) {
        Write-Host "[COVER] No cover.jpg found, skipping"
        return $output_file
    }    
    
    $output_file_with_cover = "$input_folder\covered.mka" # Prepare the covered MKA filepath

    if (Test-Path -Path $output_file_with_cover) { Remove-Item $output_file_with_cover } # Delete existing covered MKA file
        
    Write-Host "[COVER] Cover file found, writing cover to $output_file" -ForegroundColor Cyan
    .\ffmpeg -i $output_file -c copy -attach $cover_file -metadata:s:t mimetype=image/jpeg $output_file_with_cover -hide_banner -loglevel error

    if (Test-Path -Path $output_file_with_cover) { # If the covered MKA was created successfully
        
        Remove-Item $output_file # Delete the previously created MKA
        Write-Host "[COVER] Cover written to '$output_file_with_cover' successfully" -ForegroundColor Green
        $output_file = $output_file_with_cover # Update current MKA file
    }
    else { Write-Host "[ERROR] Error writing cover version of '$output_file'" -ForegroundColor Red } # If the covered MKA was not created successfully

    return $output_file
}

# Main script execution
$input_folder = $args[0]

ValidateInputDirectory($input_folder)

$author, $title = ParseDirectoryName($input_folder)

# Combine MP3s
$output_file = Combine $input_folder

# Add chapters
$chapters_file = "$input_folder\chapters.txt"
$output_file = Chapterize $output_file $chapters_file

# Add cover art
$cover_file = "$input_folder\cover.jpg"
$output_file = Cover $output_file $cover_file

# Check final MKA file existence
if (Test-Path -Path $output_file) { 
    $new_output = "$input_folder\$author - $title.mka"
    Move-Item -Path $output_file -Destination $new_output
    Write-Host "[INFO] '$new_output' written successfully!" -ForegroundColor Green
}
else { Write-Host "[ERROR] Error writing '$output_file'" -ForegroundColor Red }

exit
