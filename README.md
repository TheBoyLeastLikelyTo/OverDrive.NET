# OverDrive.NET

Collection of tools dedicated to cataloging audiobooks obtained from OverDrive 

## Known Bugs

### FFMPEG

- accented characters in title and author not supported by FFMPEG
- dashes in title/author as such ` - ` are interpreted by script as divider, some books have these

### CHAPTERS

- Certain books, instead of including "      " for elimination, include null FFFFFF
- Certain books have a different time format. Not sure if there is any relation between them

### Downloader

- Colon in book title not permitted for use with Windows Directory name


