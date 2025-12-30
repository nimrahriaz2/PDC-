using System;
using System.Drawing;
using System.IO;
using System.Threading;

namespace ParallelImageProcessing
{
    /// <summary>
    /// Module 1: Main / CLI Controller
    /// Parses command-line arguments, validates inputs, orchestrates serial and parallel execution
    /// </summary>
    internal class Program
    {
        private static void Main(string[] args)
        {
            try
            {
                PrintHeader();

                // If no arguments provided, allow interactive input to avoid runtime error
                if (args == null || args.Length == 0)
                {
                    args = GetInteractiveArguments();
                }

                // Parse and validate command-line arguments
                var parameters = ParseArguments(args);
                string imagePath = parameters.ImagePath;
                string filterName = parameters.FilterName;
                int kernelSize = parameters.KernelSize;
                int numThreads = parameters.NumThreads;
                int timeoutMs = parameters.TimeoutMs;

                ValidateInputs(imagePath, filterName, kernelSize, numThreads, timeoutMs);

                PrintParameters(parameters);

                // Initialize modules
                var imageIO = new ImageIO();
                var filters = new Filters();
                var parallelExecutor = new ParallelExecutor(filters);
                var statistics = new Statistics();
                
                // Set statistics output directory to match input image directory
                statistics.SetBaseDirectory(imagePath);

                // Load image
                Console.WriteLine($"\n[Info] Loading image: {imagePath}");
                Bitmap originalImage = imageIO.LoadImage(imagePath);
                Console.WriteLine($"[Info] Image loaded: {originalImage.Width}x{originalImage.Height} pixels");

                // Create cancellation token source with timeout
                var cts = timeoutMs > 0 ? new CancellationTokenSource(timeoutMs) : new CancellationTokenSource();

                // Execute serial version
                Console.WriteLine($"\n[Info] Executing {filterName} filter (SERIAL)...");
                Bitmap serialResult = ExecuteFilterSerial(filters, originalImage, filterName, kernelSize, statistics);
                long serialTime = statistics.StopTiming();

                string serialOutputName = $"{filterName.ToLower()}-serial.jpg";
                string serialOutputPath = imageIO.SaveImage(serialResult, serialOutputName);
                Console.WriteLine($"[Ok] Serial execution completed in {serialTime} ms");
                Console.WriteLine($"[Ok] Saved: {serialOutputPath}");

                // Execute parallel version
                Console.WriteLine($"\n[Info] Executing {filterName} filter (PARALLEL, {numThreads} threads)...");
                Bitmap parallelResult = ExecuteFilterParallel(parallelExecutor, originalImage, filterName, kernelSize, numThreads, cts.Token, statistics);
                long parallelTime = statistics.StopTiming();

                string parallelOutputName = $"{filterName.ToLower()}-parallel.jpg";
                string parallelOutputPath = imageIO.SaveImage(parallelResult, parallelOutputName);
                Console.WriteLine($"[Ok] Parallel execution completed in {parallelTime} ms");
                Console.WriteLine($"[Ok] Saved: {parallelOutputPath}");

                // Calculate and display statistics
                double speedup = statistics.ComputeSpeedup(serialTime, parallelTime);
                double efficiency = statistics.ComputeEfficiency(speedup, numThreads);

                Console.WriteLine("\n" + "=".PadRight(60, '='));
                Console.WriteLine("PERFORMANCE STATISTICS");
                Console.WriteLine("=".PadRight(60, '='));
                Console.WriteLine($"Serial Time:   {serialTime,10} ms");
                Console.WriteLine($"Parallel Time: {parallelTime,10} ms");
                Console.WriteLine($"Speedup:       {speedup,10:F4}x");
                Console.WriteLine($"Efficiency:    {efficiency,10:F4} ({efficiency * 100:F2}%)");
                Console.WriteLine("=".PadRight(60, '='));

                // Save statistics report
                statistics.SaveReport(filterName, kernelSize, numThreads, serialTime, parallelTime, imagePath);
                statistics.SaveSummaryReport(imagePath, numThreads, kernelSize);

                Console.WriteLine("\n[Ok] Processing completed successfully!");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\n[Error] Operation was cancelled due to timeout.");
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"\n[Error] Invalid argument: {ex.Message}");
                PrintUsage();
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"\n[Error] File not found: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[Error] {ex.Message}");
                Console.WriteLine($"\nStack trace: {ex.StackTrace}");
            }
        }

        // Interactive argument entry to allow running without command-line parameters
        private static string[] GetInteractiveArguments()
        {
            Console.WriteLine("No command-line arguments provided. Enter parameters interactively.");
            Console.Write("Image path: ");
            string? imagePath = Console.ReadLine();
            Console.Write("Filter (blur, grayscale, median, invert): ");
            string? filter = Console.ReadLine();
            Console.Write("Kernel size (0 for non-kernel filters): ");
            string? kernel = Console.ReadLine();
            Console.Write("Number of threads: ");
            string? threads = Console.ReadLine();
            Console.Write("Timeout ms (optional, press Enter to skip): ");
            string? timeout = Console.ReadLine();

            imagePath ??= string.Empty;
            filter ??= string.Empty;
            kernel ??= "0";
            threads ??= "1";

            if (string.IsNullOrWhiteSpace(timeout))
            {
                return new[] { imagePath, filter, kernel, threads };
            }

            return new[] { imagePath, filter, kernel, threads, timeout };
        }

        private static void PrintHeader()
        {
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine("PARALLEL IMAGE PROCESSING CONSOLE APPLICATION");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine("Version: 2.0");
            Console.WriteLine("A complete console-based application demonstrating parallel computing");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();
        }

        private static void PrintUsage()
        {
            Console.WriteLine("\nUsage:");
            Console.WriteLine("  ParallelImageProcessing.exe <image_path> <filter> <kernel_size> <num_threads> [timeout_ms]");
            Console.WriteLine("\nParameters:");
            Console.WriteLine("  image_path   : Path to input image (PNG, JPG, BMP)");
            Console.WriteLine("  filter       : Filter name (blur, grayscale, median, invert)");
            Console.WriteLine("  kernel_size  : Kernel size for blur/median (must be odd, >= 3)");
            Console.WriteLine("                 Use 0 for grayscale and invert filters");
            Console.WriteLine("  num_threads  : Number of threads for parallel execution (>= 1)");
            Console.WriteLine("  timeout_ms   : Optional timeout in milliseconds (default: no timeout)");
            Console.WriteLine("\nExamples:");
            Console.WriteLine("  ParallelImageProcessing.exe image.jpg blur 5 4");
            Console.WriteLine("  ParallelImageProcessing.exe image.png grayscale 0 8 30000");
            Console.WriteLine("  ParallelImageProcessing.exe image.bmp median 7 4");
        }

        private static Parameters ParseArguments(string[] args)
        {
            if (args.Length < 4)
            {
                throw new ArgumentException("Insufficient arguments provided.");
            }

            // Trim surrounding whitespace and quotes from inputs (users may paste paths with quotes)
            string rawImagePath = args[0]?.Trim() ?? string.Empty;
            rawImagePath = rawImagePath.Trim('"', '\'');

            string rawFilter = args[1]?.Trim() ?? string.Empty;
            rawFilter = rawFilter.Trim('"', '\'').ToLower();

            string rawKernel = args[2]?.Trim() ?? string.Empty;
            rawKernel = rawKernel.Trim('"', '\'');

            string rawThreads = args[3]?.Trim() ?? string.Empty;
            rawThreads = rawThreads.Trim('"', '\'');

            if (string.IsNullOrEmpty(rawImagePath))
                throw new ArgumentException("Image path cannot be empty.");

            var parameters = new Parameters
            {
                ImagePath = rawImagePath,
                FilterName = rawFilter
            };

            if (!int.TryParse(rawKernel, out int kernelSize))
            {
                throw new ArgumentException($"Invalid kernel size: {args[2]}");
            }
            parameters.KernelSize = kernelSize;

            if (!int.TryParse(rawThreads, out int numThreads))
            {
                throw new ArgumentException($"Invalid number of threads: {args[3]}");
            }
            parameters.NumThreads = numThreads;

            parameters.TimeoutMs = args.Length > 4 && int.TryParse(args[4]?.Trim().Trim('"', '\''), out int timeout) ? timeout : 0;

            return parameters;
        }

        private static void ValidateInputs(string imagePath, string filterName, int kernelSize, int numThreads, int timeoutMs)
        {
            // Validate image path
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException($"Image file not found: {imagePath}");
            }

            // Validate filter name
            string[] validFilters = { "blur", "grayscale", "median", "invert" };
            if (Array.IndexOf(validFilters, filterName) == -1)
            {
                throw new ArgumentException($"Invalid filter name: {filterName}. Valid filters: {string.Join(", ", validFilters)}");
            }

            // Validate kernel size for filters that require it
            if ((filterName == "blur" || filterName == "median"))
            {
                if (kernelSize < 3 || kernelSize % 2 == 0)
                {
                    throw new ArgumentException($"Kernel size must be an odd number >= 3 for {filterName} filter. Got: {kernelSize}");
                }
            }
            else if (kernelSize != 0)
            {
                Console.WriteLine($"[Warning] Kernel size ignored for {filterName} filter (not required)");
            }

            // Validate thread count
            if (numThreads < 1)
            {
                throw new ArgumentException($"Number of threads must be >= 1. Got: {numThreads}");
            }

            // Validate timeout
            if (timeoutMs < 0)
            {
                throw new ArgumentException($"Timeout must be >= 0. Got: {timeoutMs}");
            }
        }

        private static void PrintParameters(Parameters parameters)
        {
            Console.WriteLine("\nConfiguration:");
            Console.WriteLine($"  Image Path:  {parameters.ImagePath}");
            Console.WriteLine($"  Filter:      {parameters.FilterName}");
            Console.WriteLine($"  Kernel Size: {(parameters.KernelSize > 0 ? $"{parameters.KernelSize}x{parameters.KernelSize}" : "N/A")}\n");
            Console.WriteLine($"  Threads:     {parameters.NumThreads}");
            Console.WriteLine($"  Timeout:     {(parameters.TimeoutMs > 0 ? $"{parameters.TimeoutMs} ms" : "None")}\n");
        }

        private static Bitmap ExecuteFilterSerial(Filters filters, Bitmap image, string filterName, int kernelSize, Statistics statistics)
        {
            statistics.StartTiming();

            return filterName switch
            {
                "blur" => filters.ApplyBlurSerial(image, kernelSize),
                "grayscale" => filters.ApplyGrayscaleSerial(image),
                "median" => filters.ApplyMedianSerial(image, kernelSize),
                "invert" => filters.ApplyInvertSerial(image),
                _ => throw new ArgumentException($"Unknown filter: {filterName}")
            };
        }

        private static Bitmap ExecuteFilterParallel(ParallelExecutor executor, Bitmap image, string filterName, int kernelSize, int numThreads, CancellationToken cancellationToken, Statistics statistics)
        {
            statistics.StartTiming();

            return filterName switch
            {
                "blur" => executor.ExecuteBlurParallel(image, kernelSize, numThreads, cancellationToken),
                "grayscale" => executor.ExecuteGrayscaleParallel(image, numThreads, cancellationToken),
                "median" => executor.ExecuteMedianParallel(image, kernelSize, numThreads, cancellationToken),
                "invert" => executor.ExecuteInvertParallel(image, numThreads, cancellationToken),
                _ => throw new ArgumentException($"Unknown filter: {filterName}")
            };
        }

        private class Parameters
        {
            public string ImagePath { get; set; }
            public string FilterName { get; set; }
            public int KernelSize { get; set; }
            public int NumThreads { get; set; }
            public int TimeoutMs { get; set; }
        }
    }
}
