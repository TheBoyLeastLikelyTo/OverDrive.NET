param (
    [string]$input_folder
)

if (-not $input_folder) {
    Write-Host "[ERROR] Please provide the input directory."
    exit
}

if (-not (Test-Path -Path $input_folder -PathType Container)) {
    Write-Host "[ERROR] Input folder does not exist."
    exit
}

if (-not (Test-Path -Path "$input_folder\*.mp3")) {
    Write-Host "[ERROR] No MP3 files found in the input folder."
    exit
}

$output_file = "$input_folder\combined.mka"
Write-Host "[INFO] Using output file path: '$output_file'"

if (Test-Path -Path $output_file) {
    Remove-Item $output_file
}

$temp_list = "temp.txt"

Get-ChildItem -Path "$input_folder\*.mp3" | ForEach-Object { "file '$($_.FullName)'" } | Set-Content -Path $temp_list

Write-Host "[INFO] Writing MP3 parts to new file, '$output_file'"
.\ffmpeg -f concat -safe 0 -i $temp_list -c copy $output_file -hide_banner -loglevel error

Remove-Item $temp_list

if (Test-Path -Path $output_file) {
    Write-Host "[INFO] All MP3 files from '$input_folder' combined into '$output_file'."
}

$chapters_file = "$input_folder\chapters.txt"
Write-Host "[INFO] Searching for chapters file: $chapters_file"

$output_file_with_chapters = "$input_folder\chaptered.mka"

if (Test-Path -Path $chapters_file) {
    Write-Host "[INFO] Chapters file found, writing chapters to new file, '$output_file_with_chapters'"
    .\ffmpeg -i $output_file -i $chapters_file -map_metadata 1 -codec copy $output_file_with_chapters -hide_banner -loglevel error
}
else
{
    exit
}

if (Test-Path -Path $output_file_with_chapters) {
    Remove-Item $output_file
    Write-Host "[INFO] Chapters written to $output_file_with_chapters successfully, original file deleted."
    exit
}