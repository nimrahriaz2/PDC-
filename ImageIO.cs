using System;
using System.Drawing;
using System.IO;

namespace ParallelImageProcessing
{
    /// <summary>
    /// Module 2: Image I/O and Data Model
    /// Handles loading and saving images, provides Image data structure.
    /// 
    /// Supports standard image formats: PNG, JPEG, BMP.
    /// Provides standardized Bitmap data structure for other modules to interact with.
    /// </summary>
    public class ImageIO
    {
        private string _outputDirectory;

        /// <summary>
        /// Loads an image from the specified file path.
        /// </summary>
        /// <param name="imagePath">Path to the image file (PNG, JPG, BMP)</param>
        /// <returns>Loaded Bitmap image</returns>
        /// <exception cref="ArgumentException">If imagePath is null or empty</exception>
        /// <exception cref="FileNotFoundException">If image file cannot be found</exception>
        public Bitmap LoadImage(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
                throw new ArgumentException("Image path cannot be null or empty.", nameof(imagePath));

            if (!File.Exists(imagePath))
                throw new FileNotFoundException($"Image file not found: {imagePath}");

            try
            {
                _outputDirectory = Path.GetDirectoryName(imagePath);
                if (string.IsNullOrEmpty(_outputDirectory))
                    _outputDirectory = Directory.GetCurrentDirectory();

                return new Bitmap(imagePath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load image: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Saves a processed image to disk.
        /// </summary>
        /// <param name="image">Image to save</param>
        /// <param name="filename">Output filename</param>
        /// <returns>Full path to saved image</returns>
        /// <exception cref="ArgumentNullException">If image is null</exception>
        /// <exception cref="ArgumentException">If filename is null or empty</exception>
        public string SaveImage(Bitmap image, string filename)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            if (string.IsNullOrEmpty(filename))
                throw new ArgumentException("Filename cannot be null or empty.", nameof(filename));

            try
            {
                string outputPath = Path.Combine(_outputDirectory, filename);
                image.Save(outputPath);
                return outputPath;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save image: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates a copy of the image for processing.
        /// </summary>
        public Bitmap CloneImage(Bitmap image)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            return new Bitmap(image);
        }
    }
}

