using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        if (Environment.OSVersion.Version < new Version(6, 1))
        {
            Console.WriteLine("[ERROR] This program requires Windows 7 or later to run.");
            return;
        }

        if (args.Length != 1)
        {
            Console.WriteLine("[ERROR] Please provide the path to the cover art JPEG.");
            return;
        }

        string inputPath = args[0];

        try
        {
            if (Directory.Exists(inputPath))
            {
                DirectorySweep(inputPath);
            }
            else if (File.Exists(inputPath) && Path.GetExtension(inputPath) == ".jpg")
            {
                FileResize(inputPath);
            }
            else
            {
                Console.WriteLine($"[INFO] Usage: <folder_path> OR <path_to_jpg>");
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] An error occured: {ex}");
        }

        return;
    }

    static void DirectorySweep(string folderPath)
    {
        string[] coverFiles = Directory.GetFiles(folderPath, "cover.jpg", SearchOption.AllDirectories);

        foreach (string coverFile in coverFiles)
        {
            FileResize(coverFile);
        }
    }

    static void FileResize(string filePath)
    {
        Image coverImage = CoverArtToSquare(filePath); // Get the stretched image
        coverImage.Save(filePath, ImageFormat.Jpeg); // Save the stretched image
        Console.WriteLine($"[INFO] Image stretched and saved to: {filePath}");
    }

    static Image CoverArtToSquare(string imagePath)
    {
        // Load the image from the specified path
        using Image image = Image.FromFile(imagePath);

        if (image.Width == image.Height)
        {
            throw new Exception("Image already square!");
        }

        int maxSize = Math.Max(image.Width, image.Height);

        // Calculate dimensions to fit the original image into the square frame
        int width = image.Width * maxSize / Math.Max(image.Width, image.Height);
        int height = image.Height * maxSize / Math.Max(image.Width, image.Height);

        // Create a new square bitmap
        Bitmap squareImage = new Bitmap(maxSize, maxSize);

        using (Graphics graphic = Graphics.FromImage(squareImage))
        {
            // Set interpolation mode to Bicubic
            graphic.InterpolationMode = InterpolationMode.High;

            // Draw the stretched image onto the square canvas
            graphic.DrawImage(image, 0, 0, maxSize, maxSize);
        }

        return squareImage;
    }
}
