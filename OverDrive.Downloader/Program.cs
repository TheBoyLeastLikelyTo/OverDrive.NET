using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace OverDrive;

class Program
{
    static readonly string OMC = "1.2.0";
    static readonly string OS = "10.11.6";
    static readonly string UserAgent = "OverDrive Media Console"; // Mobile app user agent

    public static void Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.WriteLine("[INFO] Usage: <odm_file>");
            return;
        }

        string targetPath = args[0];

        if (!File.Exists(targetPath))
        {
            Console.WriteLine(Messages.ErrorMessage("File", "provided does not exist!"));
            return;
        }

        Audiobook book = new(targetPath)
        {
            SaveMeta = false,
            SaveLic = true,
            DownloadParts = true,
            DownloadCover = true,
            Return = true
        };

        book.DownloadBookFromOdm();

        return;
    }

    public struct Audiobook
    {
        private ODM Odm;
        private License License;

        public Audiobook(string odmPath) : this() => OdmPath = odmPath;
        private readonly string FolderName => Sanitize($"{Odm.Metadata.Creator} - {Odm.Metadata.Title}");
        private readonly string BookFolder => Path.Combine(Directory.GetParent(OdmPath).ToString(), FolderName);
        private readonly string LicensePath => Path.Combine(BookFolder, $"{FolderName}.license");
        private readonly string MetadataPath => Path.Combine(BookFolder, $"{FolderName}.metadata");
        private readonly string CoverPath => Path.Combine(BookFolder, "cover.jpg");

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
                ParseOdm(); // Parse the contents of the ODM

                if (WriteToDisk) { CreateBookDir(); } // If any files are written, create a folder for them
                if (SaveMeta) { SaveMetadata(); } // Save the Metadata chunk of ODM to an independent file
                if (DownloadParts) { GetLicense(); } // Get the license only if the MP3 parts are desired
                if (SaveLic) { SaveLicense(); } // Save the License obtained from the OD server to an independent file
                if (DownloadParts) { GetParts(); } // Download the individual MP3 parts
                if (DownloadCover) { GetCover(); } // Download the JPG cover of the book
                if (Return) { ReturnLoan(); } // Return the loan
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private void ParseOdm() => Odm = new ODM(OdmPath);
        private readonly void CreateBookDir() => Directory.CreateDirectory(BookFolder); // Create directory for the book
        private readonly void SaveMetadata() => File.WriteAllText(MetadataPath, Odm.Metadata.RawMetadata); // Save ODM metadata block
        private readonly void SaveLicense() => File.WriteAllText(LicensePath, License.LicenseContents); // Save book license
        private void GetLicense() => License = new License(LicensePath, Odm); // Parse or Download a license for the book

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

        private readonly void GetCover() => Odm.Metadata.DownloadCover(CoverPath); // Download the book cover

        private readonly void ReturnLoan() => Odm.ReturnLoan(); // Return the book to the library it was obtained from

        private static string Sanitize(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            string sanitized = string.Concat(input.Split(invalidChars.ToCharArray()));

            return sanitized;
        }
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
            List<Task<bool>> downloads = new();

            int maxDigits = Parts.Count.ToString().Length;

            for (int i = 0; i < Parts.Count; i++)
            {
                Part currentPart = Parts[i];

                string serverUrl = $"{BaseUrl}/{currentPart.FileName}";
                string localFileName = $"Part {currentPart.Number.ToString().PadLeft(maxDigits, '0')}.mp3";
                string localPath = Path.Combine(bookRoot, localFileName);
                downloads.Add(currentPart.DownloadPart(serverUrl, localPath, lice));
            }

            await Task.WhenAll(downloads);
            int successCount = downloads.Count(task => task.Result);
            Console.WriteLine(Messages.DownloadMessage($"{successCount}/{Parts.Count} parts"));
        }

        public readonly void ReturnLoan()
        {
            // Prepare the HTTP client
            using HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

            // Try to get an HTTP response
            HttpResponseMessage response = httpClient.GetAsync(ReturnUrl).Result;

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[LOAN] Couldn't Return Loan, status code: {response.StatusCode}");
                return;
            }

            Console.WriteLine("[LOAN] Loan Returned");
            return;
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

        public async readonly Task<bool> DownloadPart(string serverUrl, string localPath, License lice)
        {
            if (File.Exists(localPath)) { File.Delete(localPath); } // Delete MP3 if it already exists

            // Prepare the HTTP client
            using HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.Add("License", lice.LicenseContents); // Add license to header
            httpClient.DefaultRequestHeaders.Add("ClientID", lice.ClientID); // Add client ID to header
            httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent); // Add OD user agent to header

            // Try to get a HTTP response
            HttpResponseMessage response = await httpClient.GetAsync(serverUrl);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(Messages.ErrorMessage("Part", $"couldn't be pulled from the server, status code: {response.StatusCode}"));
            }

            // Download the part itself
            using Stream contentStream = await response.Content.ReadAsStreamAsync();
            using FileStream fileStream = File.Create(localPath);
            await contentStream.CopyToAsync(fileStream);

            // Check that the downloaded part matches the expected size written in ODM
            (long actualSize, long expectedSize) = VerifyPart(localPath);
            bool partVerified = actualSize == expectedSize;

            // Write part, once downloaded, to console
            Console.WriteLine(Messages.DownloadMessage($"{Name} ... {(partVerified ? "Verified!" : $"Expected: {expectedSize}, Actual: {actualSize}")}"));

            return File.Exists(localPath); // Return true if downloaded part exists
        }

        public readonly (long, long) VerifyPart(string localPath)
        {
            FileInfo fileInfo = new(localPath);
            long actualSize = fileInfo.Length; // Size of file on disk
            long expectedSize = FileSize; // ODM expected size

            return (actualSize, expectedSize);
        }
    }

    public readonly struct Metadata
    {
        public readonly string RawMetadata;

        public Metadata(string fileContents) => RawMetadata = fileContents;

        private readonly XDocument MetadataXml => XDocument.Parse(RawMetadata);
        private string GetElementValue(string xpath) => MetadataXml.XPathSelectElement(xpath)?.Value ?? throw new Exception(Messages.ErrorMessage("Metadata", $"XML element '{xpath}' {(MetadataXml.XPathSelectElement(xpath) == null ? "null!" : "value null!")}"));
        public string Title => GetElementValue("//Title");
        public string Creator => GetElementValue("//Creator[starts-with(@role, 'Author')]");
        public string CoverUrl => GetElementValue("//CoverUrl");

        public readonly void DownloadCover(string localPath)
        {
            // Prepare the HTTP client
            using HttpClient client = new();

            // Try to get a HTTP response
            HttpResponseMessage response = client.GetAsync(CoverUrl).Result;

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine(Messages.ErrorMessage("Cover", $"couldn't be pulled from the server, status code: {response.StatusCode}"));
                return;
            }

            // Download the cover itself
            using Stream stream = response.Content.ReadAsStreamAsync().Result;
            using FileStream fileStream = File.Create(localPath);
            stream.CopyTo(fileStream);

            // Write cover, once downloaded, to console
            Console.WriteLine(Messages.DownloadMessage("cover"));
        }
    }

    public readonly struct License
    {
        private readonly string RawLicense;

        public License(string localPath, ODM Odm)
        {
            if (File.Exists(localPath))
            {
                RawLicense = GetFromFile(localPath);
                Console.WriteLine("[LICENSE] License read from file");
            }
            else
            {
                RawLicense = GetFromServer(Odm);
                Console.WriteLine($"[LICENSE] License acquired from server");
            }
        }

        private static string GetFromFile(string localPath) => File.ReadAllText(localPath);

        private static string GetFromServer(ODM data)
        {
            // Generate Client GUID (random generation)
            Guid ClientID = Guid.NewGuid();

            // Get and print AcquisitionUrl (from ODM file)
            string AcquisitionUrl = data.AcquisitionUrl;

            // Get and print MediaID (from ODM file)
            string MediaID = data.MediaID;
            // Calculate hash
            string Hash = Convert.ToBase64String(SHA1.Create().ComputeHash(Encoding.Unicode.GetBytes($"{ClientID}|{OMC}|{OS}|ELOSNOC*AIDEM*EVIRDREVO")));

            // Construct URL for license download
            string requestUrl = $"{AcquisitionUrl}?MediaID={MediaID}&ClientID={ClientID}&OMC={OMC}&OS={OS}&Hash={Hash}";

            // Return license contents
            return Download(requestUrl);
        }

        private static string Download(string fileUrl)
        {
            // Prepare the HTTP client
            using HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

            // Try to get a HTTP response
            HttpResponseMessage response = httpClient.GetAsync(fileUrl).Result;

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine(Messages.ErrorMessage("License", $"couldn't be pulled from the server, status code: {response.StatusCode}"));
            }

            Console.WriteLine(Messages.DownloadMessage("license")); // Write success to console
            return response.Content.ReadAsStringAsync().Result; // Return license contents
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
        public static string DownloadMessage(string product) => $"[DOWNLOAD] Downloaded {product}";
    }
}
