using ATL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace OverDrive;

public class Program
{
    public static void Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.WriteLine($"[INFO] Usage: <book_folder>");
            return;
        }

        string inputPath = args[0];

        if (!Directory.Exists(inputPath))
        {
            Console.WriteLine("[ERROR] Provided directory does not exist!");
            return;
        }

        // Create array of targeted audio files
        string[] FilePaths = Directory.GetFiles(inputPath, "*.mp3");

        // Check if directory contains any target files
        if (FilePaths.Length == 0)
        {
            // If no files in directory
            Console.WriteLine($"[ERROR] No MP3 files in specified folder!");
            return;
        }

        // Create a new audiobook object to store the parsed book
        Audiobook book = new();

        try
        {
            // Set the created book to one comprising all the file parts
            book = new Audiobook(FilePaths);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }

        /* Create XML chapters file
        XDocument Final = book.CreateXml();
        Final.Save(Path.Combine(folderPath, "chapters.xml"));
        */

        // Create txt chapters file
        string txt = book.CreateFFMPEG();
        File.WriteAllText(Path.Combine(inputPath, "chapters.txt"), txt);

        // Quit the program
        return;
    }

    public readonly struct AudioFile
    {
        private readonly Track track;

        public AudioFile(string path) => track = new Track(path);
        private readonly string MediaMarkerPath => $"{Path.ChangeExtension(FileName, "xml")}";
        public readonly string FileName => track.Path;
        public readonly TimeSpan Duration => TimeSpan.FromSeconds(track.Duration);
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
                .Select(node => new MediaMarker(node)) // Return a MediaMarker of a particular node
                .ToList() ?? throw new Exception("MediaMarkers tag doesn't contain valid XML!"); // If null, error
            }
        }

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
    }

    public readonly struct MediaMarker
    {
        private readonly XmlNode markerNode;

        public MediaMarker(XmlNode Node) => markerNode = Node;
        public readonly string Name => markerNode.SelectSingleNode("Name")?.InnerText ?? throw new Exception($"[ERROR] MediaMarker XML contains no 'Name' field!");
        private readonly string StartTime => markerNode.SelectSingleNode("Time")?.InnerText ?? throw new Exception($"[ERROR] MediaMarker XML contains no 'Time' field!");
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
        private readonly MediaMarker marker;

        public Chapter(MediaMarker Marker) => marker = Marker;
        public readonly string Name => marker.Name;
        public TimeSpan AbridgedTime { get; set; }
        public readonly bool Eliminate => Name.Contains("      ");
        public override string ToString()
        {
            return $"    {Name} == {marker.UnabridgedTime} ==> {AbridgedTime}{(Eliminate ? " ELIMINATED" : "")}";
        }
    }

    private struct Audiobook
    {
        private readonly string[] FilePaths;

        public Audiobook(string[] Files)
        {
            FilePaths = Files;
            //WriteMarkersToFileMass();
            CalculateChapters();
        }

        private void CalculateChapters()
        {
            // Create TimeSpan to track seek position into multiple MP3's combined times
            TimeSpan AbridgedSeekPosition = new(); // Starts at Zero

            Chaps = Files.SelectMany(MP3 => // For each MP3 file in the files list:
            {
                Console.WriteLine($"'{Path.GetFileName(MP3.FileName)}' ({AbridgedSeekPosition}):"); // Print file name and duration

                // Duration of this MP3, and all previously parsed MP3s
                AbridgedSeekPosition += MP3.Duration; // before processing the MP3's markers, add MP3 duration to seek position

                return MP3.MediaMarkers.Select(marker => // For each MediaMarker in this MP3 file:
                {
                    // CalculatedChapterStart = (Duration of all MP3s including this one) - (duration of this part) + (duration into this part of marker)

                    // Calculate abridged start time based on the above formula
                    TimeSpan CalculatedChapterStart = AbridgedSeekPosition - MP3.Duration + marker.UnabridgedTime;

                    // Create chapter object from this marker
                    Chapter chap = new(marker)
                    {
                        // Set abridged start time to calculated start time
                        AbridgedTime = CalculatedChapterStart
                    };

                    Console.WriteLine(chap.ToString());

                    return chap;
                });
            })
            .ToList();
        }

        public readonly List<AudioFile> Files => FilePaths.Select(path => new AudioFile(path)).ToList();
        private List<Chapter>? Chaps;
        private readonly List<Chapter> Chapters => Chaps ?? throw new Exception("[ERROR] Couldn't calculate abridged chapter timings. Chapter list returned null.");

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
            int paddingLength = Chapters.Count.ToString().Length;

            StringBuilder builder = new();

            for (int i = 0; i < Chapters.Count; i++)
            {
                Chapter currentChap = Chapters[i];

                if (currentChap.Eliminate)
                {
                    continue;
                }

                string chapterIndex = (i + 1).ToString($"D{paddingLength}"); // Ensure two-digit index with leading zeros
                string chapterTime = currentChap.AbridgedTime.ToString("hh\\:mm\\:ss\\.fff");
                builder.Append($"CHAPTER{chapterIndex}={chapterTime}\nCHAPTER{chapterIndex}NAME={currentChap.Name}");
            }

            return builder.ToString();
        }

        public string CreateFFMPEG()
        {
            StringBuilder builder = new();

            builder.AppendLine($";FFMETADATA1");
            builder.AppendLine();

            for (int i = 0; i < Chapters.Count; i++)
            {
                Chapter currentChap = Chapters[i];

                if (currentChap.Eliminate)
                {
                    continue;
                }

                double time = currentChap.AbridgedTime.TotalMilliseconds;
                double endTime;

                if (i == Chapters.Count - 1)
                {
                    endTime = time;
                }
                else
                {
                    endTime = Chapters[i + 1].AbridgedTime.TotalMilliseconds;
                }

                builder.AppendLine($"[CHAPTER]");
                builder.AppendLine($"TIMEBASE=1/1000");
                builder.AppendLine($"START={time}");
                builder.AppendLine($"END={endTime}");
                builder.AppendLine($"title={currentChap.Name}");
                builder.AppendLine();
            }

            return builder.ToString();
        }
    }
}