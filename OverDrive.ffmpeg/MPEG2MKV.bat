@echo off
setlocal enabledelayedexpansion

if "%~1"=="" (
    echo Please provide the input directory.
    exit /b
)

set "input_folder=%~1"
set "output_file=output.mka"

if not exist "%input_folder%" (
    echo Input folder does not exist.
    exit /b
)

if not exist "%input_folder%\*.mp3" (
    echo No MP3 files found in the input folder.
    exit /b
)

if exist "%output_file%" (
    del "%output_file%"
)

set "temp_list=temp.txt"

(for %%i in ("%input_folder%\*.mp3") do echo file '%%i') > "%temp_list%"

ffmpeg -f concat -safe 0 -i "%temp_list%" -c copy "%output_file%"

"%temp_list%"

echo All MP3 files from "%input_folder%" combined into "%output_file%"
