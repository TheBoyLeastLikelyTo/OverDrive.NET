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

        if (!(File.Exists(inputPath) && Path.GetExtension(inputPath) == ".jpg"))
        {
            Console.WriteLine($"[INFO] Usage: <folder_path> OR <path_to_jpg>");
        }

        try
        {
            CoverArtToSquare(inputPath); // Get the stretched image

            if (CoverImage != null)
            {
                CoverImage.Save(inputPath, ImageFormat.Jpeg); // Save the stretched image
                Console.WriteLine($"[INFO] Image stretched and saved to: '{inputPath}'");
            }
            else
            {
                Console.WriteLine($"[ERROR] '{inputPath}' is already square!");
            }

            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] An error occured: {ex}");
        }

        return;
    }

    public static void CoverArtToSquare(string imagePath)
    {
        // Load the image from the specified path
        using Image image = Image.FromFile(imagePath);

        if (image.Width == image.Height)
        {
            CoverImage = null;
            return;
        }

        int maxSize = Math.Max(image.Width, image.Height);

        // Calculate dimensions to fit the original image into the square frame
        int width = image.Width * maxSize / Math.Max(image.Width, image.Height);
        int height = image.Height * maxSize / Math.Max(image.Width, image.Height);

        // Create a new square bitmap
        Bitmap squareImage = new(maxSize, maxSize);

        using (Graphics graphic = Graphics.FromImage(squareImage))
        {
            // Set interpolation mode to Bicubic
            graphic.InterpolationMode = InterpolationMode.High;

            // Draw the stretched image onto the square canvas
            graphic.DrawImage(image, 0, 0, maxSize, maxSize);
        }

        CoverImage = squareImage;

        return;
    }

    public static Image? CoverImage { get; set; }
}
