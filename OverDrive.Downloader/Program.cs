using System;
using System.Collections.Generic;
using System.Data;
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
        if (args.Length != 1)
        {
            Console.WriteLine("[INFO] Usage: <odm_file> OR <directory_of_odms");
            return;
        }

        string targetPath = args[0];

        if (!File.Exists(targetPath) && !Directory.Exists(targetPath))
        {
            Console.WriteLine(Messages.ErrorMessage("File or directory", "provided does not exist!"));
            return;
        }

        string[] odmPaths = targetPath.EndsWith(".odm") ? new string[] { targetPath } : Directory.GetFiles(targetPath, "*.odm");

        foreach (string odmPath in odmPaths)
        {
            Audiobook book = new(odmPath)
            {
                SaveMeta = false,
                SaveLic = false,
                DownloadParts = true,
                DownloadCover = true,
                Return = true
            };

            book.DownloadBookFromOdm();
        }

        return;
    }

    public struct Audiobook
    {
        private ODM Odm;
        private License License;

        public Audiobook(string odmPath) : this() => OdmPath = odmPath;
        private readonly string FolderName => $"{Odm.Metadata.Creator} - {Odm.Metadata.Title}";
        private readonly string BookFolder => Path.Combine(Directory.GetParent(OdmPath).ToString(), FolderName);
        private readonly string LicensePath => Path.Combine(BookFolder, $"{FolderName}.license");
        private readonly string MetadataPath => Path.Combine(BookFolder, $"{FolderName}.metadata");

        private string OdmPath { get; set; }
        public bool SaveMeta { get; set; }
        public bool SaveLic { get; set; }
        public bool DownloadParts { get; set; }
        public bool DownloadCover { get; set; }
        public bool Return { get; set; }
        public readonly bool WriteToDisk => SaveMeta || SaveLic || DownloadParts || DownloadCover;

        public void DownloadBookFromOdm()
        {
            try
            {
                ParseOdm();

                if (WriteToDisk) { CreateBookDir(); }
                if (SaveMeta) { SaveMetadata(); }
                if (DownloadParts) { GetLicense(); }
                if (SaveLic) { SaveLicense(); }
                if (DownloadParts) { GetParts(); }
                if (DownloadCover) { GetCover(); }
                if (Return) { ReturnLoan(); }
            }
            catch (Exception ex)
            {
                Console.WriteLine(Messages.ErrorMessage("Book", $"couldn't be downloaded: {ex}"));
            }
        }

        private void ParseOdm() => Odm = new ODM(OdmPath);
        private readonly void CreateBookDir() => Directory.CreateDirectory(BookFolder);
        private readonly void SaveMetadata() => Odm.Metadata.ToFile(MetadataPath);
        private readonly void SaveLicense() => License.ToFile(LicensePath);
        private void GetLicense() => License = new License(!File.Exists(LicensePath), LicensePath, Odm);

        private readonly void GetParts()
        {
            ODM oDM = Odm;
            string folder = BookFolder;
            License lic = License;

            // Download all the parts
            Task.Run(async () =>
            {
                await oDM.DownloadParts(folder, lic);
            }).GetAwaiter().GetResult();
        }

        private readonly void GetCover()
        {
            // Download the cover
            string coverPath = Path.Combine(BookFolder, "cover.jpg");
            Console.WriteLine($"[FILE] Cover Path: {coverPath}");
            Odm.Metadata.DownloadCover(coverPath);
            Console.WriteLine("[FILE] Cover saved to file");
        }

        private readonly void ReturnLoan() => Odm.ReturnLoan();
    }

    public readonly struct ODM
    {
        private readonly string RawOdm;

        public ODM(string filePath) => RawOdm = File.ReadAllText(filePath);
        private readonly XmlDocument OdmXml => new() { InnerXml = RawOdm };
        private XmlNode GetNode(string NodeName) => OdmXml.SelectSingleNode(NodeName) ?? throw new Exception(Messages.ErrorMessage("ODM", $"doesn't contain expected node: '{NodeName}'"));
        private static XmlAttribute GetAttribute(XmlNode node, string attributeName) => node?.Attributes?[attributeName] ?? throw new Exception(Messages.ErrorMessage("ODM", $"doesn't contain expected attribute: '{attributeName}'"));
        private static string GetInnerText(XmlNode node) => node?.InnerText ?? throw new Exception(Messages.ErrorMessage("ODM", $"doesn't contain necessary InnerText: {node}"));
        public string MediaID => GetAttribute(OdmXml.DocumentElement ?? throw new Exception(Messages.ErrorMessage("ODM", "contains no root tree element!")), "id")?.Value ?? throw new Exception(Messages.ErrorMessage("ODM", "doesn't contain expected attribute: 'id'"));
        public string AcquisitionUrl => GetInnerText(GetNode("//License/AcquisitionUrl"));
        public Metadata Metadata => new(GetNode("//License/following-sibling::text()[1]").Value ?? throw new Exception(Messages.ErrorMessage("ODM", "doesn't contain expected Metadata tree")));
        public List<Part> Parts => OdmXml.SelectNodes("//Part")?.Cast<XmlNode>().Select(node => new Part(node)).ToList() ?? throw new Exception(Messages.ErrorMessage("ODM", "contains no parts!"));
        public string BaseUrl => GetAttribute(GetNode("//Protocol"), "baseurl").Value ?? throw new Exception(Messages.ErrorMessage("ODM", "doesn't contain expected attribute: 'baseurl'"));
        public string ReturnUrl => GetInnerText(GetNode("//EarlyReturnURL"));

        public async readonly Task DownloadParts(string bookRoot, License lice)
        {
            List<Task> downloadTasks = new();

            int maxDigits = Parts.Count.ToString().Length;

            for (int i = 0; i < Parts.Count; i++)
            {
                Part currentPart = Parts[i];

                string serverUrl = $"{BaseUrl}/{currentPart.FileName}";
                string saveAsName = $"Part {currentPart.Number.ToString().PadLeft(maxDigits, '0')}.mp3";
                downloadTasks.Add(currentPart.DownloadPart(serverUrl, Path.Combine(bookRoot, saveAsName), lice));
            }

            await Task.WhenAll(downloadTasks);
            Console.WriteLine(Messages.DownloadMessage($"{Parts.Count} parts"));
        }

        public readonly void ReturnLoan()
        {
            using HttpClient httpClient = new();
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
    }

    public readonly struct Part
    {
        private readonly XmlNode Parts;

        public Part(XmlNode partNode) => Parts = partNode;
        private readonly string? GetAttributeValue(string attributeName) => Parts.Attributes?[attributeName]?.Value;
        private readonly T GetAttributeValue<T>(string attributeName, Func<string, T> parser) => parser(GetAttributeValue(attributeName) ?? throw new Exception(Messages.ErrorMessage("Parts", $"XML attribute, '{attributeName}', is null and can't be parsed!"))) ?? throw new Exception(Messages.ErrorMessage("Parts", "XML attributes couldn't be parsed!"));
        public readonly double Number => GetAttributeValue("number", double.Parse);
        public readonly long FileSize => GetAttributeValue("filesize", long.Parse);
        public readonly string Name => GetAttributeValue("name", s => s);
        public readonly string FileName => GetAttributeValue("filename", s => s.Replace("{", "%7B").Replace("}", "%7D"));
        public readonly string Duration => GetAttributeValue("duration", s => s);

        public async readonly Task DownloadPart(string serverUrl, string localPath, License lice)
        {
            using HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.Add("License", lice.LicenseContents);
            httpClient.DefaultRequestHeaders.Add("ClientID", lice.ClientID);
            httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);

            HttpResponseMessage response = await httpClient.GetAsync(serverUrl);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(Messages.ErrorMessage("Part", $"couldn't be pulled from the server, return code: {response.StatusCode}"));
            }

            using Stream contentStream = await response.Content.ReadAsStreamAsync();
            using FileStream fileStream = File.Create(localPath);
            await contentStream.CopyToAsync(fileStream);
            Console.Write(Messages.DownloadMessage(Name));
            VerifyPart(localPath);
        }

        public readonly void VerifyPart(string localPath)
        {
            FileInfo fileInfo = new(localPath);
            long fileSize = fileInfo.Length;
            long expectedSize = FileSize;

            if (fileSize == expectedSize)
            {
                Console.Write("... Verified!");
            }
            else
            {
                Console.Write($"... Not verified, Size Expected: {expectedSize}, Actual Size: {fileSize}");
            }

            Console.WriteLine();
        }
    }

    public readonly struct Metadata
    {
        private readonly string RawMetadata;

        public Metadata(string fileContents) => RawMetadata = fileContents ?? throw new ArgumentNullException(nameof(fileContents));

        public readonly void ToFile(string filePath)
        {
            Console.WriteLine(Messages.FileMessage("Metadata", filePath));
            File.WriteAllText(filePath, RawMetadata);
        }

        private readonly XDocument MetadataXml => XDocument.Parse(RawMetadata);
        private string GetElementValue(string xpath) => MetadataXml.XPathSelectElement(xpath)?.Value ?? throw new Exception(Messages.ErrorMessage("Metadata", $"XML element '{xpath}' {(MetadataXml.XPathSelectElement(xpath) == null ? "null!" : "value null!")}"));
        private static string SanitizeString(string? input) => input is null ? throw new ArgumentNullException(nameof(input), "Input string cannot be null.") : Regex.Replace(input, @"[^a-zA-Z0-9\s\._-]", "-").Trim('-', ' ');
        public string Title => SanitizeString(GetElementValue("//Title"));
        public string Creator => GetElementValue("//Creator[starts-with(@role, 'Author')]");
        public string CoverUrl => GetElementValue("//CoverUrl");

        public readonly void DownloadCover(string localPath)
        {
            using HttpClient client = new();
            HttpResponseMessage response = client.GetAsync(CoverUrl).Result;

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine(Messages.ErrorMessage("Cover", $"couldn't be pulled from the server, return code: {response.StatusCode}"));
                return;
            }

            using Stream stream = response.Content.ReadAsStreamAsync().Result;
            using FileStream fileStream = File.Create(localPath);
            stream.CopyTo(fileStream);

            Console.WriteLine(Messages.DownloadMessage("cover"));
        }
    }

    public readonly struct License
    {
        private readonly string RawLicense;

        public License(bool getFromServer, string localPath, ODM Odm)
        {
            Console.WriteLine(Messages.FileMessage("License", localPath));
            if (getFromServer)
            {
                Console.WriteLine($"[LICENSE] No license found, trying to get one from the server...");
                RawLicense = GetFromServer(Odm);
                Console.WriteLine($"[LICENSE] License acquired from server!");
            }
            else
            {
                RawLicense = localPath ?? throw new Exception(Messages.ErrorMessage("License", "loaded from file is null and cannot be used!"));
                RawLicense = File.ReadAllText(RawLicense);
                Console.WriteLine("[LICENSE] License read from file");
            }
        }

        private static string GetFromServer(ODM data)
        {
            // Generate Client GUID (random generation)
            Guid ClientID = Guid.NewGuid();

            // Get and print AcquisitionUrl (from ODM file)
            string AcquisitionUrl = data.AcquisitionUrl;

            // Get and print MediaID (from ODM file)
            string MediaID = data.MediaID;

            // Calculate hash
            string RawHash = $"{ClientID}|{OMC}|{OS}|ELOSNOC*AIDEM*EVIRDREVO";
            using SHA1 sha1 = SHA1.Create();
            string Hash = Convert.ToBase64String(sha1.ComputeHash(Encoding.Unicode.GetBytes(RawHash)));

            // Construct URL for license request
            string requestUrl = $"{AcquisitionUrl}?MediaID={MediaID}&ClientID={ClientID}&OMC={OMC}&OS={OS}&Hash={Hash}";

            return DownloadLicense(requestUrl);
        }

        private static string DownloadLicense(string fileUrl)
        {
            using HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            HttpResponseMessage response = httpClient.GetAsync(fileUrl).Result;

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine(Messages.ErrorMessage("License", $"couldn't be pulled from the server, return code: {response.StatusCode}"));
            }

            Console.WriteLine(Messages.DownloadMessage("license"));
            return response.Content.ReadAsStringAsync().Result;
        }

        public readonly void ToFile(string filePath)
        {
            File.WriteAllText(filePath, RawLicense);
        }

        public readonly string LicenseContents => RawLicense ?? throw new Exception(Messages.ErrorMessage("License", "contents returned null!"));
        private readonly XDocument LicenseXml => XDocument.Parse(LicenseContents);
        private static XNamespace LicenseNamespace => "http://license.overdrive.com/2008/03/License.xsd";
        private readonly string GetElementValue(string elementName) => LicenseXml.Root?.Element(LicenseNamespace + "SignedInfo")?.Element(LicenseNamespace + elementName)?.Value ?? throw new Exception(Messages.ErrorMessage("License", "contains one or more XML values that couldn't be parsed"));
        public readonly string Version => GetElementValue("Version");
        public readonly string ContentID => GetElementValue("ContentID");
        public readonly string ClientID => GetElementValue("ClientID");
        public readonly string Signature => LicenseXml.Root?.Element(LicenseNamespace + "Signature")?.Value ?? throw new Exception(Messages.ErrorMessage("License", "'Signature' XML tag couldn't be parsed"));
    }

    public class Messages
    {
        public static string ErrorMessage(string product, string message) => $"[ERROR] {product} {message}";
        public static string DownloadMessage(string product) => $"[DOWNLOAD] Downloaded {product} successfully!";
        public static string FileMessage(string product, string filePath) => $"[FILE] {product} Path: '{filePath}'";
    }
}
