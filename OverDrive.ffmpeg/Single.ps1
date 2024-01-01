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

$input_folder = $args[0]

ValidateInputDirectory $input_folder

$author, $title = ParseDirectoryName $input_folder

Write-Host "[INFO] Current Book: $title by $author"

$output_file = Join-Path -Path $input_folder -ChildPath "combined.mka"

$temp_list = "temp.txt"
Get-ChildItem -Path "$input_folder\*.mp3" | ForEach-Object { "file '$($_.FullName -replace "'", "'\''")'" } | Set-Content -Path $temp_list

$cover_file = Join-Path -Path $input_folder -ChildPath "cover.jpg"
$chapters_file = Join-Path -Path $input_folder -ChildPath "chapters.txt"

if (Test-Path -Path $output_file) {
    Remove-Item $output_file
}

$commandMeta = "-metadata title=`"$title`" -metadata artist=`"$author`" -metadata album=`"$title`""
$commandCover = " -attach `"$cover_file`" -metadata:s:t mimetype=image/jpeg -metadata:s:t filename=cover.jpg"
$commandChapters = "-i `"$chapters_file`" "

$midCommand = switch ($true) {
    { (-not (Test-Path -Path $cover_file)) -and (-not (Test-Path -Path $chapters_file)) } {
        Write-Host "[INFO] No Chapters or Cover found." -ForegroundColor Yellow
        $commandMeta
    }
    { (Test-Path -Path $cover_file) -and (-not (Test-Path -Path $chapters_file)) } {
        Write-Host "[INFO] Cover found, no Chapters found." -ForegroundColor Yellow
        $commandMeta + $commandCover
    }
    { (Test-Path -Path $chapters_file) -and (-not (Test-Path -Path $cover_file)) } {
        Write-Host "[INFO] Chapters found, no Cover found." -ForegroundColor Yellow
        $commandChapters + $commandMeta
    }
    { (Test-Path -Path $chapters_file) -and (Test-Path -Path $cover_file) } {
        Write-Host "[INFO] Cover and Chapters found." -ForegroundColor Cyan
        $commandChapters + $commandMeta + $commandCover
    }
    default { "" }
}

$fullCommand = ".\ffmpeg -f concat -safe 0 -i `"$temp_list`" $midCommand -c copy `"$output_file`" -hide_banner -loglevel error"

Write-Host "[INFO] Combining..." -ForegroundColor Cyan
Invoke-Expression $fullCommand

Remove-Item $temp_list

if (Test-Path -Path $output_file) {
    $new_output = "$input_folder\$title.mka"
    Copy-Item -Path $output_file -Destination $new_output
	Remove-Item -Path $output_file
    Write-Host "[INFO] Successfully combined '$title' by '$author'" -ForegroundColor Green
} else {
    Write-Host "[ERROR] Failure combining '$title' by '$author'" -ForegroundColor Red
}

exit
