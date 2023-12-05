@echo off
setlocal

set "input_folder=%~1"

if "%input_folder%"=="" (
    echo [ERROR] Please provide the input directory.
    exit /b
)

if not exist "%input_folder%" (
    echo [ERROR] Input folder does not exist.
    exit /b
)

if not exist "%input_folder%\*.mp3" (
    echo [ERROR] No MP3 files found in the input folder.
    exit /b
)

set "output_file=%input_folder%\combined.mka"
echo [INFO] Using output file path: '%output_file%'

if exist "%output_file%" (
    del "%output_file%"
)

set "temp_list=temp.txt"

for %%F in ("%input_folder%\*.mp3") do echo file '%%~fF' >> %temp_list%

echo [INFO] Writing MP3 parts to new file, '%output_file%'
ffmpeg -f concat -safe 0 -i %temp_list% -c copy %output_file% -hide_banner -loglevel error

del %temp_list%

if exist "%output_file%" (
    echo [INFO] All MP3 files from '%input_folder%' combined into '%output_file%'.
)

set "chapters_file=%input_folder%\chapters.txt"
echo [INFO] Searching for chapters file: %chapters_file%

set "output_file_with_chapters=%input_folder%\chaptered.mka"

if exist "%chapters_file%" (
    echo [INFO] Chapters file found, writing chapters to new file, '%output_file_with_chapters%'
    ffmpeg -i %output_file% -i %chapters_file% -map_metadata 1 -codec copy %output_file_with_chapters% -hide_banner -loglevel error
) else (
    exit /b
)

if exist "%output_file_with_chapters%" (
    del "%output_file%"
    echo [INFO] Chapters written to %output_file_with_chapters% successfully, original file deleted.
    exit /b
)
