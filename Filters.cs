using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;

namespace ParallelImageProcessing
{
    /// <summary>
    /// Module 3: Filter Algorithms
    /// Implements all image processing filters in both serial and parallel versions.
    /// 
    /// PCAM Methodology:
    /// - Partitioning: Image decomposed into horizontal strips (rows)
    /// - Communication: For kernel-based filters (blur, median), threads access ghost cells
    ///   (border rows from adjacent partitions) via shared buffer. For point-wise filters
    ///   (grayscale, invert), no communication needed.
    /// - Agglomeration: Pixel-level tasks grouped into row strips
    /// - Mapping: Static block-wise assignment to threads
    /// </summary>
    public class Filters
    {
        /// <summary>
        /// Applies blur filter using serial processing.
        /// </summary>
        public Bitmap ApplyBlurSerial(Bitmap image, int kernelSize)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            ValidateKernelSize(image, kernelSize);

            var result = new Bitmap(image);
            var rectangle = new Rectangle(0, 0, result.Width, result.Height);
            var data = result.LockBits(rectangle, ImageLockMode.ReadWrite, result.PixelFormat);
            int colorDepth = Bitmap.GetPixelFormatSize(data.PixelFormat) / 8;
            byte[] buffer = new byte[result.Width * result.Height * colorDepth];

            Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

            int startX = kernelSize / 2;
            int endX = data.Width - kernelSize / 2;
            int startY = kernelSize / 2;
            int endY = data.Height - kernelSize / 2;

            ProcessBlur(buffer, kernelSize, startX, endX, startY, endY, data.Width, colorDepth);

            Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
            result.UnlockBits(data);

            return result;
        }

        /// <summary>
        /// Applies blur filter using parallel processing (called by ParallelExecutor).
        /// 
        /// Ghost Cell Access: This method processes pixels in range [startY, endY), but when
        /// computing the blur for pixels near boundaries, it reads from adjacent rows outside
        /// this range (ghost cells). Since all threads share the same buffer and only read
        /// (not write) from ghost cells, this is thread-safe.
        /// </summary>
        public void ApplyBlurParallel(byte[] buffer, int kernelSize, int startX, int endX, int startY, int endY, int width, int colorDepth)
        {
            ProcessBlur(buffer, kernelSize, startX, endX, startY, endY, width, colorDepth);
        }

        /// <summary>
        /// Applies grayscale conversion using serial processing.
        /// </summary>
        public Bitmap ApplyGrayscaleSerial(Bitmap image)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            var result = new Bitmap(image);
            var rectangle = new Rectangle(0, 0, result.Width, result.Height);
            var data = result.LockBits(rectangle, ImageLockMode.ReadWrite, result.PixelFormat);
            int colorDepth = Bitmap.GetPixelFormatSize(data.PixelFormat) / 8;
            byte[] buffer = new byte[result.Width * result.Height * colorDepth];

            Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

            ProcessGrayscale(buffer, 0, data.Width, 0, data.Height, data.Width, colorDepth);

            Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
            result.UnlockBits(data);

            return result;
        }

        /// <summary>
        /// Applies grayscale conversion using parallel processing (called by ParallelExecutor).
        /// </summary>
        public void ApplyGrayscaleParallel(byte[] buffer, int startX, int endX, int startY, int endY, int width, int colorDepth)
        {
            ProcessGrayscale(buffer, startX, endX, startY, endY, width, colorDepth);
        }

        /// <summary>
        /// Applies median filter using serial processing.
        /// </summary>
        public Bitmap ApplyMedianSerial(Bitmap image, int kernelSize)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            ValidateKernelSize(image, kernelSize);

            var result = new Bitmap(image);
            var rectangle = new Rectangle(0, 0, result.Width, result.Height);
            var data = result.LockBits(rectangle, ImageLockMode.ReadWrite, result.PixelFormat);
            int colorDepth = Bitmap.GetPixelFormatSize(data.PixelFormat) / 8;
            byte[] buffer = new byte[result.Width * result.Height * colorDepth];

            Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

            int startX = kernelSize / 2;
            int endX = data.Width - kernelSize / 2;
            int startY = kernelSize / 2;
            int endY = data.Height - kernelSize / 2;

            ProcessMedianFilter(buffer, kernelSize, startX, endX, startY, endY, data.Width, colorDepth);

            Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
            result.UnlockBits(data);

            return result;
        }

        /// <summary>
        /// Applies median filter using parallel processing (called by ParallelExecutor).
        /// 
        /// Ghost Cell Access: Similar to blur, this method accesses ghost cells from adjacent
        /// partitions when computing median values near boundaries.
        /// </summary>
        public void ApplyMedianParallel(byte[] buffer, int kernelSize, int startX, int endX, int startY, int endY, int width, int colorDepth)
        {
            ProcessMedianFilter(buffer, kernelSize, startX, endX, startY, endY, width, colorDepth);
        }

        /// <summary>
        /// Applies color inversion using serial processing.
        /// </summary>
        public Bitmap ApplyInvertSerial(Bitmap image)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            var result = new Bitmap(image);
            var rectangle = new Rectangle(0, 0, result.Width, result.Height);
            var data = result.LockBits(rectangle, ImageLockMode.ReadWrite, result.PixelFormat);
            int colorDepth = Bitmap.GetPixelFormatSize(data.PixelFormat) / 8;
            byte[] buffer = new byte[result.Width * result.Height * colorDepth];

            Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

            ProcessInvert(buffer, 0, data.Width, 0, data.Height, data.Width, colorDepth);

            Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
            result.UnlockBits(data);

            return result;
        }

        /// <summary>
        /// Applies color inversion using parallel processing (called by ParallelExecutor).
        /// </summary>
        public void ApplyInvertParallel(byte[] buffer, int startX, int endX, int startY, int endY, int width, int colorDepth)
        {
            ProcessInvert(buffer, startX, endX, startY, endY, width, colorDepth);
        }

        #region Private Processing Methods

        private void ValidateKernelSize(Bitmap image, int kernelSize)
        {
            if (kernelSize < 1 || kernelSize % 2 == 0)
                throw new ArgumentException("Kernel size must be a positive odd number.", nameof(kernelSize));

            if (kernelSize > image.Width || kernelSize > image.Height)
                throw new ArgumentException($"Kernel size ({kernelSize}) cannot exceed image dimensions ({image.Width}x{image.Height}).", nameof(kernelSize));
        }

        /// <summary>
        /// Processes blur filter on a region of the image buffer.
        /// Processes pixels in [startY, endY) range. When computing kernel operations near boundaries,
        /// reads from ghost cells (adjacent rows) which are safe to access since all threads share
        /// the same buffer and only read (not write) from those cells.
        /// </summary>
        private void ProcessBlur(byte[] buffer, int kernelSize, int startX, int endX, int startY, int endY, int width, int colorDepth)
        {
            for (int x = startX; x < endX; x++)
            {
                for (int y = startY; y < endY; y++)
                {
                    CalculateAverageRgb(buffer, x, y, kernelSize, width, colorDepth);
                }
            }
        }

        /// <summary>
        /// Calculates average RGB values for a pixel using a kernel window.
        /// Accesses pixels in the kernel window, which may include ghost cells from adjacent partitions.
        /// </summary>
        private void CalculateAverageRgb(byte[] buffer, int x, int y, int kernelSize, int width, int colorDepth)
        {
            int sumR = 0, sumG = 0, sumB = 0;
            int kernelArea = kernelSize * kernelSize;

            for (int i = x - (kernelSize / 2); i <= x + (kernelSize / 2); i++)
            {
                for (int j = y - (kernelSize / 2); j <= y + (kernelSize / 2); j++)
                {
                    int offset = ((j * width) + i) * colorDepth;
                    sumR += buffer[offset];
                    sumG += buffer[offset + 1];
                    sumB += buffer[offset + 2];
                }
            }

            int currentPixel = ((y * width) + x) * colorDepth;
            buffer[currentPixel] = (byte)(sumR / kernelArea);
            buffer[currentPixel + 1] = (byte)(sumG / kernelArea);
            buffer[currentPixel + 2] = (byte)(sumB / kernelArea);
        }

        private void ProcessGrayscale(byte[] buffer, int startX, int endX, int startY, int endY, int width, int colorDepth)
        {
            // Grayscale equation: 0.2126 * R + 0.7152 * G + 0.0722 * B
            for (int x = startX; x < endX; x++)
            {
                for (int y = startY; y < endY; y++)
                {
                    int offset = ((y * width) + x) * colorDepth;
                    byte grayscale = (byte)(0.2126f * buffer[offset] + 0.7152f * buffer[offset + 1] + 0.0722f * buffer[offset + 2]);
                    buffer[offset] = buffer[offset + 1] = buffer[offset + 2] = grayscale;
                }
            }
        }

        /// <summary>
        /// Processes median filter on a region of the image buffer.
        /// Similar to blur, accesses ghost cells when computing median values near boundaries.
        /// </summary>
        private void ProcessMedianFilter(byte[] buffer, int kernelSize, int startX, int endX, int startY, int endY, int width, int colorDepth)
        {
            for (int x = startX; x < endX; x++)
            {
                for (int y = startY; y < endY; y++)
                {
                    int offset = ((y * width) + x) * colorDepth;
                    byte median = CalculateRgbMedian(buffer, x, y, kernelSize, width, colorDepth);
                    buffer[offset] = buffer[offset + 1] = buffer[offset + 2] = median;
                }
            }
        }

        private byte CalculateRgbMedian(byte[] buffer, int x, int y, int kernelSize, int width, int colorDepth)
        {
            List<byte> rChannel = new List<byte>();

            for (int i = x - (kernelSize / 2); i <= x + (kernelSize / 2); i++)
            {
                for (int j = y - (kernelSize / 2); j <= y + (kernelSize / 2); j++)
                {
                    int offset = ((j * width) + i) * colorDepth;
                    rChannel.Add(buffer[offset]);
                }
            }

            return rChannel.OrderBy(v => v).ElementAt(rChannel.Count / 2);
        }

        private void ProcessInvert(byte[] buffer, int startX, int endX, int startY, int endY, int width, int colorDepth)
        {
            for (int x = startX; x < endX; x++)
            {
                for (int y = startY; y < endY; y++)
                {
                    int offset = ((y * width) + x) * colorDepth;
                    buffer[offset] = (byte)(255 - buffer[offset]);
                    buffer[offset + 1] = (byte)(255 - buffer[offset + 1]);
                    buffer[offset + 2] = (byte)(255 - buffer[offset + 2]);
                }
            }
        }

        #endregion
    }
}


