param (
    [string]$input_folder
)

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

$output_file = "$input_folder\combined.mka"
Write-Host "[MP3 PARTS] Using output file path: '$output_file'" -ForegroundColor Cyan

$temp_list = "temp.txt"

Get-ChildItem -Path "$input_folder\*.mp3" | ForEach-Object { "file '$($_.FullName)'" } | Set-Content -Path $temp_list

if (Test-Path -Path $output_file) {
    Remove-Item $output_file
}

Write-Host "[MP3 PARTS] Writing MP3 parts to new file, '$output_file'" -ForegroundColor Cyan
.\ffmpeg -f concat -safe 0 -i $temp_list -c copy $output_file -hide_banner -loglevel error

Remove-Item $temp_list

if (Test-Path -Path $output_file) {
    Write-Host "[MP3 PARTS] All MP3 files from '$input_folder' combined into '$output_file'." -ForegroundColor Green
}
else {
    Write-Host "[ERROR] Error combining MP3 parts to '$output_file'" -ForegroundColor Red
}

$chapters_file = "$input_folder\chapters.txt"
Write-Host "[CHAPTERS] Searching for chapters file: $chapters_file" -ForegroundColor Cyan
$output_file_with_chapters = "$input_folder\chaptered.mka"

# Check whether the user has a chapters file in the book directory
if (Test-Path -Path $chapters_file) {
    
    if (Test-Path -Path $output_file_with_chapters) {
        # If a chaptered mka has already been written, delete it
        Remove-Item $output_file_with_chapters
    }
    
    Write-Host "[CHAPTERS] Chapters file found, writing chapters to new file, '$output_file_with_chapters'" -ForegroundColor Cyan
    .\ffmpeg -i $output_file -i $chapters_file -map_metadata 1 -codec copy $output_file_with_chapters -hide_banner -loglevel error

    # Ensure ffmpeg saved a chapterized mka file
    if (Test-Path -Path $output_file_with_chapters) {
        # If a mka with chapters exists:

        Remove-Item $output_file # Delete the original mka
        Write-Host "[CHAPTERS] Chapters written to '$output_file_with_chapters' successfully, original file deleted." -ForegroundColor Green
        $output_file = $output_file_with_chapters # Update current final mka file
    }
    else {
        Write-Host "[ERROR] Error writing chaptered version of '$output_file'" -ForegroundColor Red
    }
}
else {
    Write-Host "[CHAPTERS] No chapters file found at $chapters_file" -ForegroundColor Red
}

$cover_file = "$input_folder\cover.jpg"
Write-Host "[COVER] Searching for cover file: $cover_file" -ForegroundColor Cyan
$output_file_with_cover = "$input_folder\covered.mka"

# Check whether the user has a cover file in the book directory
if (Test-Path -Path $cover_file) {
    
    if (Test-Path -Path $output_file_with_cover) {
        # If a covered mka file has already been written, delete it
        Remove-Item $output_file_with_cover
    }
    
    Write-Host "[COVER] Cover file found, writing cover to $output_file" -ForegroundColor Cyan
    .\ffmpeg -i $output_file -i $cover_file -map 0 -map 1 -c copy -metadata:s:v filename="cover.jpg" -metadata:s:v mimetype="image/jpeg" $output_file_with_cover -hide_banner -loglevel error

    # Ensure ffmpeg saved a covered mka file
    if (Test-Path -Path $output_file_with_cover) {
        # If a mka with a cover exists:
        
        Remove-Item $output_file # Delete the original mka
        Write-Host "[COVER] Cover written to '$output_file_with_cover' successfully, original file deleted." -ForegroundColor Green
        $output_file = $output_file_with_cover # Update the current final mka file
    }
    else {
        Write-Host "[ERROR] Error writing cover version of '$output_file'" -ForegroundColor Red
    }
}
else {
    Write-Host "[COVER] No cover file found at '$cover_file'" -ForegroundColor Red
}

# Ensure a final file has been written
if (Test-Path -Path $output_file) {
    Write-Host "[INFO] '$output_file' written successfully!" -ForegroundColor Green
}
else {
    Write-Host "[ERROR] Error writing '$output_file'" -ForegroundColor Red
}

exit