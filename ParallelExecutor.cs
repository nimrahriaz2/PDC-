using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ParallelImageProcessing
{
    /// <summary>
    /// Module 4: Parallel Execution Manager
    /// Manages thread creation, row-based partitioning, and synchronization.
    /// 
    /// PCAM Methodology Implementation:
    /// - Partitioning: Data decomposition into horizontal strips (rows of pixels)
    /// - Communication: Shared buffer allows access to ghost cells (border rows) for kernel-based filters
    /// - Agglomeration: Pixel-level tasks grouped into row strips to minimize overhead
    /// - Mapping: Static block-wise mapping of strips to CPU cores/threads
    /// </summary>
    public class ParallelExecutor
    {
        private readonly Filters _filters;

        public ParallelExecutor(Filters filters)
        {
            _filters = filters ?? throw new ArgumentNullException(nameof(filters));
        }

        /// <summary>
        /// Executes blur filter in parallel using row-based partitioning.
        /// 
        /// Ghost Cell Handling: Each thread processes a horizontal strip but can read from
        /// adjacent partitions (ghost cells) when computing kernel-based operations near boundaries.
        /// Since all threads share the same buffer array, they can safely read from anywhere,
        /// but only write to pixels within their assigned partition.
        /// </summary>
        public Bitmap ExecuteBlurParallel(Bitmap image, int kernelSize, int numThreads, CancellationToken cancellationToken = default)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            ValidateThreadCount(numThreads);

            var result = new Bitmap(image);
            var rectangle = new Rectangle(0, 0, result.Width, result.Height);
            var data = result.LockBits(rectangle, ImageLockMode.ReadWrite, result.PixelFormat);
            int colorDepth = Bitmap.GetPixelFormatSize(data.PixelFormat) / 8;
            byte[] buffer = new byte[result.Width * result.Height * colorDepth];

            Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

            int startX = kernelSize / 2;
            int endX = data.Width - kernelSize / 2;
            int halfKernel = kernelSize / 2;

            var options = new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = numThreads };

            // PCAM: Partitioning - Divide image into horizontal strips
            // Each thread processes rows [startY, endY), but can read from ghost cells
            // (adjacent rows) when computing kernel operations near boundaries
            Parallel.For(0, numThreads, options, (threadId) =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Static block-wise mapping: divide rows evenly among threads
                int rowsPerThread = data.Height / numThreads;
                int startY = (threadId * rowsPerThread) + halfKernel;
                int endY = threadId == numThreads - 1 
                    ? data.Height - halfKernel 
                    : startY + rowsPerThread;

                // Communication: Thread reads from shared buffer (including ghost cells from adjacent partitions)
                // but only writes to pixels within its assigned partition [startY, endY)
                _filters.ApplyBlurParallel(buffer, kernelSize, startX, endX, startY, endY, data.Width, colorDepth);
            });

            Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
            result.UnlockBits(data);

            return result;
        }

        /// <summary>
        /// Executes grayscale filter in parallel using row-based partitioning.
        /// 
        /// Point-wise operation: No ghost cells needed since each pixel is processed independently.
        /// Communication is minimal - only synchronization at the end to confirm all tasks complete.
        /// </summary>
        public Bitmap ExecuteGrayscaleParallel(Bitmap image, int numThreads, CancellationToken cancellationToken = default)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            ValidateThreadCount(numThreads);

            var result = new Bitmap(image);
            var rectangle = new Rectangle(0, 0, result.Width, result.Height);
            var data = result.LockBits(rectangle, ImageLockMode.ReadWrite, result.PixelFormat);
            int colorDepth = Bitmap.GetPixelFormatSize(data.PixelFormat) / 8;
            byte[] buffer = new byte[result.Width * result.Height * colorDepth];

            Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

            var options = new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = numThreads };

            // PCAM: Point-wise operation - no ghost cells needed
            Parallel.For(0, numThreads, options, (threadId) =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                int rowsPerThread = data.Height / numThreads;
                int startY = threadId * rowsPerThread;
                int endY = threadId == numThreads - 1 ? data.Height : startY + rowsPerThread;

                _filters.ApplyGrayscaleParallel(buffer, 0, data.Width, startY, endY, data.Width, colorDepth);
            });

            Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
            result.UnlockBits(data);

            return result;
        }

        /// <summary>
        /// Executes median filter in parallel using row-based partitioning.
        /// 
        /// Ghost Cell Handling: Similar to blur filter, each thread needs access to border rows
        /// from adjacent partitions when computing median values near boundaries.
        /// </summary>
        public Bitmap ExecuteMedianParallel(Bitmap image, int kernelSize, int numThreads, CancellationToken cancellationToken = default)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            ValidateThreadCount(numThreads);

            var result = new Bitmap(image);
            var rectangle = new Rectangle(0, 0, result.Width, result.Height);
            var data = result.LockBits(rectangle, ImageLockMode.ReadWrite, result.PixelFormat);
            int colorDepth = Bitmap.GetPixelFormatSize(data.PixelFormat) / 8;
            byte[] buffer = new byte[result.Width * result.Height * colorDepth];

            Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

            int startX = kernelSize / 2;
            int endX = data.Width - kernelSize / 2;
            int halfKernel = kernelSize / 2;

            var options = new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = numThreads };

            // PCAM: Partitioning with ghost cell support for kernel-based operations
            Parallel.For(0, numThreads, options, (threadId) =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                int rowsPerThread = data.Height / numThreads;
                int startY = (threadId * rowsPerThread) + halfKernel;
                int endY = threadId == numThreads - 1 
                    ? data.Height - halfKernel 
                    : startY + rowsPerThread;

                _filters.ApplyMedianParallel(buffer, kernelSize, startX, endX, startY, endY, data.Width, colorDepth);
            });

            Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
            result.UnlockBits(data);

            return result;
        }

        /// <summary>
        /// Executes invert filter in parallel using row-based partitioning.
        /// 
        /// Point-wise operation: No ghost cells needed since each pixel is processed independently.
        /// Communication is minimal - only synchronization at the end to confirm all tasks complete.
        /// </summary>
        public Bitmap ExecuteInvertParallel(Bitmap image, int numThreads, CancellationToken cancellationToken = default)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            ValidateThreadCount(numThreads);

            var result = new Bitmap(image);
            var rectangle = new Rectangle(0, 0, result.Width, result.Height);
            var data = result.LockBits(rectangle, ImageLockMode.ReadWrite, result.PixelFormat);
            int colorDepth = Bitmap.GetPixelFormatSize(data.PixelFormat) / 8;
            byte[] buffer = new byte[result.Width * result.Height * colorDepth];

            Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

            var options = new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = numThreads };

            // PCAM: Point-wise operation - no ghost cells needed
            Parallel.For(0, numThreads, options, (threadId) =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                int rowsPerThread = data.Height / numThreads;
                int startY = threadId * rowsPerThread;
                int endY = threadId == numThreads - 1 ? data.Height : startY + rowsPerThread;

                _filters.ApplyInvertParallel(buffer, 0, data.Width, startY, endY, data.Width, colorDepth);
            });

            Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
            result.UnlockBits(data);

            return result;
        }

        private void ValidateThreadCount(int numThreads)
        {
            if (numThreads < 1)
                throw new ArgumentException("Number of threads must be greater than 0.", nameof(numThreads));

            if (numThreads > Environment.ProcessorCount)
            {
                Console.WriteLine($"[Warning] Requested {numThreads} threads but only {Environment.ProcessorCount} processors available.");
            }
        }
    }
}

