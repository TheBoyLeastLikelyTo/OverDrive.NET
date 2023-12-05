using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace OverDrive;

public class Program
{
    static readonly string OMC = "1.2.0";
    static readonly string OS = "10.11.6";
    static readonly string UserAgent = "OverDrive Media Console"; // Mobile app user agent

    public static void Main(string[] args)
    {
        string odmPath = "";

        if (args.Length != 1)
        {
            Console.WriteLine($"[INFO] Usage: <odm_file>");
            return;
        }
        else if (File.Exists(args[0]))
        {
            odmPath = args[0];
        }
        else
        {
            Console.WriteLine("[ERROR] Provided file does not exist!");
            return;
        }
        
        Audiobook.FromOdm(odmPath);

        return;
    }

    public struct Audiobook
    {
        public static Audiobook FromOdm(string odmPath)
        {
            Audiobook book = new()
            {
                OdmPath = odmPath
            };

            book.Odm = ODM.New(book.OdmPath);

            book.DownloadBook();

            return book;
        }

        public readonly string BookFolder
        {
            get
            {
                return Path.Combine(Directory.GetParent(OdmPath).ToString(), FolderName);
            }
        }

        private readonly string FolderName
        {
            get
            {
                return $"{Odm.Metadata.Creator} - {Odm.Metadata.Title}";
            }
        }

        private void DownloadBook()
        {
            Directory.CreateDirectory(BookFolder);

            HandleMetadata(Path.Combine(BookFolder, $"{FolderName}.metadata"));

            HandleLicense(Path.Combine(BookFolder, $"{FolderName}.license"));

            HandleCover(Path.Combine(BookFolder, "cover.jpg"));

            DownloadParts(BookFolder);

            ReturnLoan();

            MoveOdm($"{BookFolder}.odm");
        }

        private void HandleLicense(string targetPath)
        {
            Console.WriteLine($"[FILE] License Path: {targetPath}");

            if (!File.Exists(targetPath))
            {
                // Get license & save it to a file

                license = License.GetFromServer(Odm);
                license.ToFile(targetPath);
                Console.WriteLine("[FILE] License saved to file");
            }
            else
            {
                license = License.GetFromFile(targetPath);
                Console.WriteLine("[LICENSE] License read from file");
            }
        }

        private readonly void DownloadParts(string folderPath)
        {
            ODM currentOdm = Odm;
            License lic = license;

            Task.Run(async () =>
            {
                await currentOdm.DownloadParts(folderPath, lic);
            }).GetAwaiter().GetResult();
        }

        private readonly void HandleMetadata(string targetPath)
        {
            Console.WriteLine($"[FILE] Metadata Path: {targetPath}");

            // Save ODM Metadata to a file
            Odm.Metadata.ToFile(targetPath);
            Console.WriteLine("[FILE] Metadata saved to file");
        }

        private readonly void HandleCover(string targetPath)
        {
            Console.WriteLine($"[FILE] Cover Path: {targetPath}");

            Odm.Metadata.DownloadCover(targetPath);
            Console.WriteLine("[FILE] Cover saved to file");
        }

        private readonly void ReturnLoan()
        {
            Odm.ReturnLoan();
        }

        private readonly void MoveOdm(string targetPath)
        {
            File.Move(OdmPath, targetPath);
            Console.WriteLine($"[FILE] ODM Relocated Path: '{Path.GetFileName(OdmPath)}' ==> '{Path.GetFileName(targetPath)}'");
        }

        private License license;

        private string OdmPath;

        private ODM Odm { get; set; }
    }


    public struct ODM
    {
        public static ODM New(string filePath)
        {
            return new ODM
            {
                RawOdm = File.ReadAllText(filePath)
            };
        }

        private string RawOdm { get; set; }

        private readonly XmlDocument OdmXml
        {
            get
            {
                XmlDocument doc = new();

                doc.LoadXml(RawOdm);

                return doc;
            }
        }

        public readonly void ReturnLoan()
        {
            using HttpClient httpClient = new();

            // Set custom User-Agent header
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

            HttpResponseMessage response = httpClient.GetAsync(ReturnUrl).Result;

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("[LOAN] Loan Returned");
            }
            else
            {
                Console.WriteLine($"[LOAN] Couldn't Return Loan. Is it already returned? Status Code: {response.StatusCode}");
            }
        }

        public async readonly Task DownloadParts(string bookRoot, License lice)
        {
            List<Task> downloadTasks = new();

            foreach (Part part in Parts)
            {
                downloadTasks.Add(DownloadPart(part, Path.Combine(bookRoot, $"{part.Name}.mp3"), lice));
            }

            await Task.WhenAll(downloadTasks);
            Console.WriteLine($"[DOWNLOAD] {Parts.Count} parts downloaded successfully");
        }

        private async readonly Task DownloadPart(Part part, string localPath, License lice)
        {
            string serverUrl = $"{BaseUrl}/{part.FileName}";

            using HttpClient httpClient = new();

            httpClient.DefaultRequestHeaders.Add("License", lice.RawLicense);
            httpClient.DefaultRequestHeaders.Add("ClientID", lice.ClientID);
            httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);

            HttpResponseMessage response = await httpClient.GetAsync(serverUrl);

            if (response.IsSuccessStatusCode)
            {
                using Stream contentStream = await response.Content.ReadAsStreamAsync();
                using FileStream fileStream = File.Create(localPath);
                await contentStream.CopyToAsync(fileStream);
                Console.WriteLine($"[DOWNLOAD] {part}");
            }
            else
            {
                throw new Exception($"[ERROR] Couldn't pull audio part from server. Status Code: {response.StatusCode}");
            }
        }

        public readonly string MediaID
        {
            get
            {
                return OdmXml.DocumentElement.GetAttribute("id");
            }
        }

        public readonly string AcquisitionUrl
        {
            get
            {
                return OdmXml.SelectSingleNode("//License/AcquisitionUrl").InnerText;
            }
        }

        public readonly Metadata Metadata
        {
            get
            {
                return Metadata.FromXml(OdmXml.SelectSingleNode("//License/following-sibling::text()[1]").Value);
            }
        }

        private readonly List<Part> Parts
        {
            get
            {
                return OdmXml.SelectNodes("//Part")
                .Cast<XmlNode>()
                .Select(Part.FromXml)
                .ToList();
            }
        }

        private readonly string BaseUrl
        {
            get
            {
                return OdmXml.SelectSingleNode("//Protocol").Attributes["baseurl"].Value;
            }
        }

        private readonly string ReturnUrl
        {
            get
            {
                return OdmXml.SelectSingleNode("//EarlyReturnURL").InnerText;
            }
        }
    }

    public struct Part
    {
        public static Part FromXml(XmlNode partNode)
        {
            return new Part()
            {
                Parts = partNode
            };
        }

        public override readonly string ToString()
        {
            string Concat = "";

            Concat += $"{Name}:";
            Concat += $"\n    Number: {Number}";
            Concat += $"\n    File Size: {FileSize}";
            Concat += $"\n    Part Name: {Name}";
            Concat += $"\n    File Name: {FileName}";
            Concat += $"\n    Duration: {Duration}";

            return Concat;
        }

        private XmlNode Parts;

        public readonly string Number
        {
            get
            {
                return Parts.Attributes["number"].Value;
            }
        }

        public readonly string FileSize
        {
            get
            {
                return Parts.Attributes["filesize"].Value;
            }
        }

        public readonly string Name
        {
            get
            {
                return Parts.Attributes["name"].Value;
            }
        }

        private readonly string FileNameRaw
        {
            get
            {
                return Parts.Attributes["filename"].Value;
            }
        }

        public readonly string FileName
        {
            get
            {
                return FileNameRaw.Replace("{", "%7B").Replace("}", "%7D");
            }
        }

        public readonly string Duration
        {
            get
            {
                return Parts.Attributes["duration"].Value;
            }
        }
    }

    public struct Metadata
    {
        public static Metadata FromXml(string fileContents)
        {
            return new Metadata()
            {
                RawMetadata = fileContents
            };
        }

        public readonly void ToFile(string filePath)
        {
            File.WriteAllText(filePath, RawMetadata);
            return;
        }

        public readonly void DownloadCover(string localPath)
        {
            using HttpClient client = new();
            HttpResponseMessage response = client.GetAsync(CoverUrl).Result;

            if (response.IsSuccessStatusCode)
            {
                using Stream stream = response.Content.ReadAsStreamAsync().Result;
                using FileStream fileStream = File.Create(localPath);
                stream.CopyTo(fileStream);
                Console.WriteLine($"[DOWNLOAD] Downloaded Cover Successfully");
            }
            else
            {
                Console.WriteLine($"[ERROR] Couldn't pull cover from server. Status Code: {response.StatusCode}");
                return;
            }
        }

        public string RawMetadata { readonly get; private set; }

        private readonly XDocument MetadataXml
        {
            get
            {
                XDocument doc = XDocument.Parse(RawMetadata);

                return doc;
            }
        }

        private readonly string TitleRaw
        {
            get
            {
                return MetadataXml.XPathSelectElement("//Title").Value;
            }
        }

        public readonly string Title
        {
            get
            {
                // Define a regex pattern to match characters other than alphanumeric, space, period, underscore, and hyphen
                string pattern = @"[^a-zA-Z0-9\s\._-]";

                // Replace non-matching characters with a hyphen
                string sanitized = Regex.Replace(TitleRaw, pattern, "-");

                // Trim leading/trailing hyphens and spaces
                sanitized = sanitized.Trim('-', ' ');

                return sanitized;
            }
        }

        public readonly string Creator
        {
            get
            {
                return MetadataXml.XPathSelectElement("//Creator[starts-with(@role, 'Author')]").Value;
            }
        }

        private readonly string CoverUrl
        {
            get
            {
                return MetadataXml.XPathSelectElement("//CoverUrl").Value;
            }
        }
    }

    public struct License
    {
        public static License GetFromServer(ODM data)
        {
            Console.WriteLine($"[LICENSE] No license found, trying to get one from the server...");

            License serverLic = new();

            // Generate Client GUID (random generation)
            Guid ClientID = Guid.NewGuid();
            Console.WriteLine($"[LICENSE] Generating random ClientID={ClientID.ToString().ToUpper()}");

            // Get and print AcquisitionUrl (from ODM file)
            string AcquisitionUrl = data.AcquisitionUrl;
            Console.WriteLine($"[LICENSE] Using AcquisitionUrl={AcquisitionUrl}");

            // Get and print MediaID (from ODM file)
            string MediaID = data.MediaID;
            Console.WriteLine($"[LICENSE] Using MediaID={MediaID}");

            // Generate RawHash (to be hashed soon) based on separated values
            string RawHash = $"{ClientID}|{OMC}|{OS}|ELOSNOC*AIDEM*EVIRDREVO";
            Console.WriteLine($"[LICENSE] Using RawHash={RawHash}");

            // Hash RawHash
            byte[] utf16Data = Encoding.Convert(Encoding.ASCII, Encoding.Unicode, Encoding.ASCII.GetBytes(RawHash)); // Convert ASCII to UTF-16 byte sequence
            using SHA1 sha1 = SHA1.Create(); // Create SHA hash object
            string Hash = Convert.ToBase64String(sha1.ComputeHash(utf16Data)); // Convert SHA hash to base-64
            Console.WriteLine($"[LICENSE] Using Hash={Hash}");

            string requestUrl = $"{AcquisitionUrl}?MediaID={MediaID}&ClientID={ClientID}&OMC={OMC}&OS={OS}&Hash={Hash}";

            serverLic.DownloadLicense(requestUrl);

            Console.WriteLine($"[LICENSE] License acquired from server!");

            return serverLic;
        }

        public static License GetFromFile(string filePath)
        {
            Console.WriteLine($"[LICENSE] Using existing license, '{Path.GetFileName(filePath)}'");

            return new License()
            {
                RawLicense = File.ReadAllText(filePath)
            };
        }

        public readonly void ToFile(string filePath)
        {
            File.WriteAllText(filePath, RawLicense);
            return;
        }

        private void DownloadLicense(string fileUrl)
        {
            using HttpClient httpClient = new();

            // Set custom User-Agent header
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

            HttpResponseMessage response = httpClient.GetAsync(fileUrl).Result;

            if (response.IsSuccessStatusCode)
            {
                RawLicense = response.Content.ReadAsStringAsync().Result;
                Console.WriteLine($"[DOWNLOAD] Downloaded License Successfully");
                return;
            }
            else
            {
                throw new Exception($"[ERROR] Couldn't pull license from server. Status Code: {response.StatusCode}");
            }
        }

        public string RawLicense { readonly get; private set; }

        private readonly XDocument LicenseXml
        {
            get
            {
                return XDocument.Parse(RawLicense);
            }
        }

        private static XNamespace LicenseNamespace
        {
            get
            {
                return "http://license.overdrive.com/2008/03/License.xsd";
            }
        }

        public readonly string Version
        {
            get
            {
                return LicenseXml.Root.Element(LicenseNamespace + "SignedInfo").Element(LicenseNamespace + "Version").Value;
            }
        }

        public readonly string ContentID
        {
            get
            {
                return LicenseXml.Root.Element(LicenseNamespace + "SignedInfo").Element(LicenseNamespace + "ContentID").Value;
            }
        }

        public readonly string ClientID
        {
            get
            {
                return LicenseXml.Root.Element(LicenseNamespace + "SignedInfo").Element(LicenseNamespace + "ClientID").Value;
            }
        }

        public readonly string Signature
        {
            get
            {
                return LicenseXml.Root.Element(LicenseNamespace + "Signature").Value;
            }
        }
    }
}
