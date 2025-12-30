using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ParallelImageProcessing
{
    /// <summary>
    /// Module 5: Statistics and Reporter
    /// Measures execution time, computes speedup and efficiency, saves results to file
    /// </summary>
    public class Statistics
    {
        private readonly Stopwatch _stopwatch;
        private string _outputPath;
        private string _baseDirectory;

        public Statistics(string outputPath = null)
        {
            _stopwatch = new Stopwatch();
            _outputPath = outputPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "image_processing_statistics.txt");
        }

        /// <summary>
        /// Sets the base directory for saving statistics files (typically the input image directory).
        /// </summary>
        public void SetBaseDirectory(string imagePath)
        {
            if (!string.IsNullOrEmpty(imagePath))
            {
                _baseDirectory = Path.GetDirectoryName(Path.GetFullPath(imagePath));
                if (string.IsNullOrEmpty(_baseDirectory))
                {
                    _baseDirectory = Directory.GetCurrentDirectory();
                }
                _outputPath = Path.Combine(_baseDirectory, "image_processing_statistics.txt");
            }
        }

        /// <summary>
        /// Starts timing measurement.
        /// </summary>
        public void StartTiming()
        {
            _stopwatch.Restart();
        }

        /// <summary>
        /// Stops timing measurement and returns elapsed time in milliseconds.
        /// </summary>
        public long StopTiming()
        {
            _stopwatch.Stop();
            return _stopwatch.ElapsedMilliseconds;
        }

        /// <summary>
        /// Computes speedup: serialTime / parallelTime
        /// </summary>
        public double ComputeSpeedup(long serialTimeMs, long parallelTimeMs)
        {
            if (parallelTimeMs == 0)
                return double.PositiveInfinity;

            return (double)serialTimeMs / parallelTimeMs;
        }

        /// <summary>
        /// Computes efficiency: speedup / numThreads
        /// </summary>
        public double ComputeEfficiency(double speedup, int numThreads)
        {
            if (numThreads == 0)
                return 0.0;

            return speedup / numThreads;
        }

        /// <summary>
        /// Saves statistics report to a text file.
        /// </summary>
        public void SaveReport(string filterName, int kernelSize, int numThreads, long serialTimeMs, long parallelTimeMs, string imagePath)
        {
            // Set output directory based on input image path if not already set
            if (string.IsNullOrEmpty(_baseDirectory))
            {
                SetBaseDirectory(imagePath);
            }

            double speedup = ComputeSpeedup(serialTimeMs, parallelTimeMs);
            double efficiency = ComputeEfficiency(speedup, numThreads);

            var report = new StringBuilder();
            report.AppendLine("=".PadRight(80, '='));
            report.AppendLine("PARALLEL IMAGE PROCESSING STATISTICS REPORT");
            report.AppendLine("=".PadRight(80, '='));
            report.AppendLine();
            report.AppendLine($"Filter: {filterName}");
            report.AppendLine($"Image: {imagePath}");
            report.AppendLine($"Kernel Size: {(kernelSize > 0 ? $"{kernelSize}x{kernelSize}" : "N/A")}");
            report.AppendLine($"Number of Threads: {numThreads}");
            report.AppendLine();
            report.AppendLine("Execution Times:");
            report.AppendLine($"  Serial Execution:   {serialTimeMs,10} ms");
            report.AppendLine($"  Parallel Execution: {parallelTimeMs,10} ms");
            report.AppendLine();
            report.AppendLine("Performance Metrics:");
            report.AppendLine($"  Speedup:    {speedup,10:F4}x");
            report.AppendLine($"  Efficiency: {efficiency,10:F4} ({efficiency * 100:F2}%)");
            report.AppendLine();
            report.AppendLine($"Report Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine("=".PadRight(80, '='));
            report.AppendLine();

            try
            {
                File.AppendAllText(_outputPath, report.ToString());
                Console.WriteLine($"[Info] Statistics saved to: {_outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to save statistics: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a summary report for all filters processed.
        /// </summary>
        public void SaveSummaryReport(string imagePath, int numThreads, int kernelSize)
        {
            var summary = new StringBuilder();
            summary.AppendLine();
            summary.AppendLine("=".PadRight(80, '='));
            summary.AppendLine("PROCESSING SESSION SUMMARY");
            summary.AppendLine("=".PadRight(80, '='));
            summary.AppendLine($"Image: {imagePath}");
            summary.AppendLine($"Kernel Size: {(kernelSize > 0 ? $"{kernelSize}x{kernelSize}" : "N/A")}");
            summary.AppendLine($"Threads Used: {numThreads}");
            summary.AppendLine($"Processors Available: {Environment.ProcessorCount}");
            summary.AppendLine($"Session Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            summary.AppendLine("=".PadRight(80, '='));
            summary.AppendLine();

            try
            {
                File.AppendAllText(_outputPath, summary.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to save summary: {ex.Message}");
            }
        }
    }
}

