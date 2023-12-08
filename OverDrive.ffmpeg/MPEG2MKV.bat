@echo off
setlocal enabledelayedexpansion

set "input_folder=%~1"

if "%input_folder%"=="" (
    echo [ERROR] Please provide the input directory.
    exit /b 1
)

if not exist "%input_folder%" (
    echo [ERROR] Input folder does not exist.
    exit /b 1
)

for %%F in ("%input_folder%\*.mp3") do (
    if "%%~xF"==".mp3" (
        set "mp3_found=true"
        exit /b 0
    )
)

if not defined mp3_found (
    echo [ERROR] No MP3 files found in the input folder.
    exit /b 1
)

set "output_file=%input_folder%\combined.mka"
echo [MP3 PARTS] Using output file path: '%output_file%'

echo. > temp.txt

for %%F in ("%input_folder%\*.mp3") do echo file '%%~fF' >> temp.txt

if exist "%output_file%" (
    del "%output_file%"
)

echo [MP3 PARTS] Writing MP3 parts to new file, '%output_file%'
ffmpeg -f concat -safe 0 -i temp.txt -c copy "%output_file%" -hide_banner -loglevel error

del temp.txt

if exist "%output_file%" (
    echo [MP3 PARTS] All MP3 files from '%input_folder%' combined into '%output_file%'.
) else (
    echo [ERROR] Error combining MP3 parts to '%output_file%'.
)

set "chapters_file=%input_folder%\chapters.txt"
echo [CHAPTERS] Searching for chapters file: %chapters_file%
set "output_file_with_chapters=%input_folder%\chaptered.mka"

if exist "%chapters_file%" (
    if exist "%output_file_with_chapters%" (
        del "%output_file_with_chapters%"
    )

    echo [CHAPTERS] Chapters file found, writing chapters to new file, '%output_file_with_chapters%'
    ffmpeg -i "%output_file%" -i "%chapters_file%" -map_metadata 1 -codec copy "%output_file_with_chapters%" -hide_banner -loglevel error

    if exist "%output_file_with_chapters%" (
        del "%output_file%"
        echo [CHAPTERS] Chapters written to '%output_file_with_chapters%' successfully, original file deleted.
        set "output_file=%output_file_with_chapters%"
    ) else (
        echo [ERROR] Error writing chaptered version of '%output_file%'.
    )
) else (
    echo [CHAPTERS] No chapters file found at %chapters_file%
)

set "cover_file=%input_folder%\cover.jpg"
echo [COVER] Searching for cover file: %cover_file%
set "output_file_with_cover=%input_folder%\covered.mka"

if exist "%cover_file%" (
    if exist "%output_file_with_cover%" (
        del "%output_file_with_cover%"
    )

    echo [COVER] Cover file found, writing cover to %output_file%
    ffmpeg -i "%output_file%" -i "%cover_file%" -map 0 -map 1 -c copy -metadata:s:v filename="cover.jpg" -metadata:s:v mimetype="image/jpeg" "%output_file_with_cover%" -hide_banner -loglevel error

    if exist "%output_file_with_cover%" (
        del "%output_file%"
        echo [COVER] Cover written to '%output_file_with_cover%' successfully, original file deleted.
        set "output_file=%output_file_with_cover%"
    ) else (
        echo [ERROR] Error writing cover version of '%output_file%'.
    )
) else (
    echo [COVER] No cover file found at '%cover_file%'
)

if exist "%output_file%" (
    echo [INFO] '%output_file%' written successfully!
) else (
    echo [ERROR] Error writing '%output_file%'
)

exit /b
