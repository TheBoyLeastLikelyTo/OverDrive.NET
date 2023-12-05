param (
    [string]$input_folder
)

if (-not $input_folder) {
    Write-Host "Please provide the input directory."
    exit
}

$output_file = "output.mka"

if (-not (Test-Path -Path $input_folder -PathType Container)) {
    Write-Host "Input folder does not exist."
    exit
}

if (-not (Test-Path -Path "$input_folder\*.mp3")) {
    Write-Host "No MP3 files found in the input folder."
    exit
}

if (Test-Path -Path $output_file) {
    Remove-Item $output_file
}

$temp_list = "temp.txt"

Get-ChildItem -Path "$input_folder\*.mp3" | ForEach-Object { "file '$($_.FullName)'" } | Set-Content -Path $temp_list

.\ffmpeg -f concat -safe 0 -i $temp_list -c copy $output_file

Remove-Item $temp_list

Write-Host "All MP3 files from '$input_folder' combined into '$output_file'."
