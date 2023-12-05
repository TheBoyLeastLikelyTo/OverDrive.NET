using ATL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace OverDrive;

public class Program
{
    public static void Main(string[] args)
    {
        string folderPath = "";
        
        if (args.Length != 1)
        {
            Console.WriteLine($"[INFO] Usage: <folder_path>");
            return;
        }
        else if (!Directory.Exists(args[0]))
        {
            folderPath = args[0];
        }
        else
        {
            Console.WriteLine("[ERROR] Provided directory does not exist!");
            return;
        }
        
        // Check if directory is valid
        if (!Directory.Exists(folderPath))
        {
            // If directory not valid, abort
            Console.WriteLine($"[ERROR] Directory '{folderPath}' is invalid!");
            return;
        }

        // Create array of targeted audio files
        string[] FilePaths = Directory.GetFiles(folderPath, "*.mp3");

        // Check if directory contains any target files
        if (FilePaths.Length == 0)
        {
            // If no files in directory
            Console.WriteLine($"[ERROR] No mp3 files in specified folder!");
            return;
        }

        // Create a new audiobook object to store the parsed book
        Audiobook book = new();

        try
        {
            // Set the created book to one comprising all the file parts
            book = Audiobook.CreateBook(FilePaths);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Error parsing gathered chapters: {ex.Message}");
        }

        // Create XML chapters file
        XDocument Final = book.CreateXml();
        Final.Save(Path.Combine(folderPath, "chapters.xml"));

        // Create txt chapters file
        string txt = book.CreateTxt();
        File.WriteAllText(Path.Combine(folderPath, "chapters.txt"), txt);

        // Quit the program
        return;
    }

    public struct AudioFile
    {
        public static AudioFile InterpretFile(string path)
        {
            return new AudioFile
            {
                track = new Track(path)
            };
        }

        private Track track;

        private readonly string MediaMarkersXml
        {
            get
            {
                KeyValuePair<string, string> MediaMarkers = new();

                try
                {
                    MediaMarkers = track.AdditionalFields
                    .FirstOrDefault(f => f.Key == "OverDrive MediaMarkers");
                }
                catch
                {
                    throw new Exception("Couldn't get MediaMarkers from mp3. Are they valid?");
                }

                if (MediaMarkers.Key == null)
                {
                    throw new Exception($"No MediaMarkers detected in '{Path.GetFileName(FileName)}'");
                }

                return MediaMarkers.Value;
            }
        }

        public readonly void WriteMarkersToFile()
        {
            // Save the original MediaMarkersXml (MP3 tag contents) to the MediaMarkerPath (save locally)
            File.WriteAllText(MediaMarkerPath, MediaMarkersXml);
        }

        private readonly string MediaMarkerPath
        {
            get
            {
                // Compile the MediaMarkerPath (where original tag stored locally) by changing the extension of MP3 file
                return $"{Path.ChangeExtension(FileName, "xml")}";
            }
        }

        public readonly string FileName
        {
            get
            {
                return track.Path; // Audio file original path
            }
        }

        public readonly TimeSpan Duration
        {
            get
            {
                return TimeSpan.FromSeconds(track.Duration); // Audio file duration parsed to TimeSpan
            }
        }

        public readonly List<MediaMarker> MediaMarkers
        {
            get
            {
                // Create new XmlDocument
                XmlDocument odChapters = new();

                if (File.Exists(MediaMarkerPath))
                {
                    // If MediaMarkers saved separately, load them instead of reading tags
                    odChapters.LoadXml(File.ReadAllText(MediaMarkerPath));
                }
                else
                {
                    // No separate markers XML, read them from MP3 tags (MediaMarkersXml)
                    odChapters.LoadXml(MediaMarkersXml);
                }

                // Create list of MediaMarkers based on XML contained in the "Markers" tag
                return odChapters.SelectNodes("/Markers/Marker") // Select the applicable node
                    ?.Cast<XmlNode>() // Convert it to XmlNode
                    .Select(MediaMarker.FromXml) // Return a MediaMarker of a particular node
                    .ToList() ?? throw new Exception("MediaMarkers tag doesn't contain valid XML!"); // If null, error
            }
        }
    }

    public struct MediaMarker
    {
        public static MediaMarker FromXml(XmlNode markerNode)
        {
            return new MediaMarker
            {
                markerNode = markerNode
            };
        }       

        private static string NullCheck(string? contents)
        {
            if (contents == null)
            {
                throw new Exception($"[ERROR] MediaMarkers XML contains null node content!");
            }
            else
            {
                return contents;
            }
        }

        private XmlNode markerNode;

        public readonly string Name
        {
            get
            {
                return NullCheck(markerNode.SelectSingleNode("Name")?.InnerText);
            }
        }

        private readonly string StartTime
        {
            get
            {
                return NullCheck(markerNode.SelectSingleNode("Time")?.InnerText);
            }
        }

        public readonly TimeSpan UnabridgedTime
        {
            get
            {
                // Example: 12:17.103

                string[] timeComponents = StartTime.Split(':'); // 12, 17.103
                string[] secondsAndMilliseconds = timeComponents[1].Split('.'); // 17, 103

                int minutes = int.Parse(timeComponents[0]); // 12

                int seconds = int.Parse(secondsAndMilliseconds[0]); // 17
                int milliseconds = int.Parse(secondsAndMilliseconds[1]); // 103

                // 0, 0, 12, 17, 103
                return new TimeSpan(0, 0, minutes, seconds, milliseconds);
            }
        }
    }

    struct Chapter
    {
        public static Chapter FromMarker(MediaMarker marker)
        {
            return new Chapter
            {
                marker = marker
            };
        }

        private MediaMarker marker;

        public readonly string Name
        {
            get
            {
                return marker.Name;
            }
        }

        public TimeSpan AbridgedTime { get; set; }

        public readonly bool Eliminate
        {
            get
            {
                // Some books include markers named with six extra spaces, these are redundant
                return Name.Contains("      ");
            }
        }

        public readonly void PrintChapter()
        {
            Console.WriteLine($"    {Name} == {marker.UnabridgedTime} ==> {AbridgedTime}{(Eliminate ? " ELIMINATED" : "")}");
        }
    }


    struct Audiobook
    {
        public static Audiobook CreateBook(string[] FilePaths)
        {
            return new Audiobook
            {
                // Create book based on FilePaths array (entry point)
                FilePaths = FilePaths
            };
        }

        private string[] FilePaths;

        public readonly List<AudioFile> Files
        {
            get
            {
                return FilePaths.Select(path => AudioFile.InterpretFile(path)).ToList();
            }
        }

        public readonly List<Chapter> Chapters
        {
            get
            {
                // Create TimeSpan to track seek position into multiple MP3's combined times
                TimeSpan AbridgedSeekPosition = new(); // Starts at Zero

                return Files.SelectMany(MP3 => // For each MP3 file in the files list:
                {
                    Console.WriteLine($"'{Path.GetFileName(MP3.FileName)}' ({AbridgedSeekPosition}):"); // Print file name and duration

                    // Duration of this MP3, and all previously parsed MP3s
                    AbridgedSeekPosition += MP3.Duration; // before processing the MP3's markers, add MP3 duration to seek position

                    return MP3.MediaMarkers.Select(marker => // For each MediaMarker in this MP3 file:
                    {
                        // UnabridgedTime = when is this MediaMarker (chapter start) within the MP3 file?

                        // To find the current chapter's start time if all MP3s combined, do the following:

                        // Say you've processed 4 hours of MP3's already, and the current one is 30 minutes
                        // Assume a particular chapter (marker) is 15 minutes (halfway) into this MP3 file

                        // For each marker in the MP3 file:
                        // Subtract 30 minutes from seek position (4 to 3.5 hours for this)
                        // Then add the duration into the MP3 of the current marker (3.5 to 3.75 hours for this)

                        // Considering the durations of all previously processed MP3 parts:
                        // This chapter is 3.75 hours into an abridged version of all the MP3s

                        // Calculate abridged start time based on the above formula
                        TimeSpan CalculatedChapterStart = AbridgedSeekPosition - MP3.Duration + marker.UnabridgedTime;

                        // Create chapter object from this marker
                        Chapter chap = Chapter.FromMarker(marker);

                        // Set abridged start time to calculated start time
                        chap.AbridgedTime = CalculatedChapterStart;

                        // Print chapter to console
                        chap.PrintChapter();

                        return chap;
                    });
                })
                .ToList();
            }
        }

        private readonly void WriteMarkersToFileMass()
        {
            // Call each AudioFile to save its MediaMarkers to a file
            Files.ForEach(file => file.WriteMarkersToFile());
        }

        public readonly XDocument CreateXml()
        {
            return new XDocument(
                new XDeclaration("1.0", null, null),
                new XElement("Chapters",
                    new XElement("EditionEntry",
                        new XElement("EditionUID", "10015869254435265348"),
                        Chapters
                            .Where(chap => !chap.Eliminate)
                            .Select(chap => new XElement("ChapterAtom",
                                new XElement("ChapterTimeStart", chap.AbridgedTime.ToString("hh\\:mm\\:ss\\.fffffff")),
                                new XElement("ChapterDisplay",
                                    new XElement("ChapterString", chap.Name),
                                    new XElement("ChapterLanguage", "und")
                                )
                            ))
                    )
                )
            );
        }

        public readonly string CreateTxt()
        {
            var chapterLines = Chapters.Select((chapter, index) =>
            {
                string chapterTime = chapter.AbridgedTime.ToString("hh\\:mm\\:ss\\.fff");
                return $"CHAPTER{index + 1}={chapterTime}\nCHAPTER{index + 1}NAME={chapter.Name}";
            });

            return string.Join("\n", chapterLines);
        }
    }
}