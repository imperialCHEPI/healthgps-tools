using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace HealthGPS.Tools.Output
{
    public class HCEDatasetBuilder
    {
        private readonly HashSet<string> factors;
        private static readonly Dictionary<string, int> identifiers = new()
        {
            {"source", -1 },
            {"run", -1 },
            {"time", -1 },
            {"count", -1},
            {"gender", -1},
            {"index_id", -1}
        };

        public HCEDatasetBuilder(OutputToolOptions config, int totalRuns)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (!config.SourceFolder.Exists)
            {
                throw new DirectoryNotFoundException($"Source data folder: {config.SourceFolder.FullName} not found.");
            }

            Delimeter = config.Delimiter;
            SourceFolder = config.SourceFolder;
            OutputFolder = config.OutputFolder;
            Options = config.HCEDataset;
            BatchRuns = config.Run.Finish;
            MaxResultFiles = config.MaxResultFiles;
            TotalRuns = totalRuns;
            AgeRange = config.Age;
            factors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (Options.FactorsFilter != null)
            {
                foreach (var factor in Options.FactorsFilter)
                {
                    factors.Add(factor.Trim());
                }
            }
        }

        public string Delimeter { get; }

        public DirectoryInfo SourceFolder { get; }

        public DirectoryInfo OutputFolder { get; }

        public DatasetOptions Options { get; }

        public Interval AgeRange { get; }

        public int BatchRuns { get; }

        public int? MaxResultFiles { get; }

        public int TotalRuns { get; }

        public int Build()
        {
            var numberOfRows = 0;
            if (Options == null)
            {
                Console.WriteLine("No HCE analysis dataset options, no action.");
                return numberOfRows;
            }

            if (Options.FactorsFilter == null || Options.FactorsFilter.Count < 1)
            {
                factors.Clear();
            }

            var outputFilename = Options.OutputFilename.Replace("{RUNS}", TotalRuns.ToString(), true, null)
                                                       .Replace("{FROM}", AgeRange.Start.ToString(), true, null)
                                                       .Replace("{TO}", AgeRange.Finish.ToString(), true, null);
            var meanDatasetFilename = Path.Combine(OutputFolder.FullName, outputFilename.Replace("{STATS}", "Mean", true, null));
            var stDevDatasetFilename = Path.Combine(OutputFolder.FullName, outputFilename.Replace("{STATS}", "StDev", true, null));
            var orderedBacthFiles = FileHelper.GetBatchResultFiles(SourceFolder, "*.csv", MaxResultFiles);

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Creating HCE dataset from {orderedBacthFiles.Count} files.");
            Console.ResetColor();

            var timer = new Stopwatch();
            timer.Start();

            using var meanStream = new StreamWriter(meanDatasetFilename);
            using var stDevStream = new StreamWriter(stDevDatasetFilename);
            var needFileHeader = true;
            foreach (var bacthFile in orderedBacthFiles)
            {
                Console.Write("  - File: {0} ... ", bacthFile.Value.FullName);
                var dataset = ProcessBatchResultsFile(bacthFile.Key, bacthFile.Value);
                if (needFileHeader)
                {
                    meanStream.Write("source,run,time,count");
                    stDevStream.Write("source,run,time,count");
                    foreach (var factor in factors)
                    {
                        meanStream.Write($"{Delimeter}{factor}");
                        stDevStream.Write($"{Delimeter}{factor}");
                    }

                    meanStream.WriteLine();
                    stDevStream.WriteLine();
                    needFileHeader = false;
                }

                foreach (var run in dataset)
                {
                    var rowKey = run.Key.Replace("_", Delimeter);
                    var rowData = run.Value;
                    var rowCount = rowData.FirstOrDefault().Value.Count;
                    meanStream.Write($"{rowKey}{Delimeter}{rowCount}");
                    stDevStream.Write($"{rowKey}{Delimeter}{rowCount}");
                    foreach (var factor in factors)
                    {
                        meanStream.Write($"{Delimeter}{rowData[factor].Mean}");
                        stDevStream.Write($"{Delimeter}{rowData[factor].StDev}");
                        if (rowData[factor].Count != rowCount)
                        {
                            throw new InvalidDataException($"Row count changed from {rowCount} to {rowData[factor].Count}.");
                        }
                    }

                    meanStream.WriteLine();
                    stDevStream.WriteLine();
                }

                numberOfRows += dataset.Count;
                Console.WriteLine($"[OK], rows: {dataset.Count}");
            }

            timer.Stop();
            Console.WriteLine();
            Console.WriteLine($"  + Outputs: {OutputFolder.FullName}");
            Console.WriteLine($"    - {Path.GetFileName(meanDatasetFilename)}.");
            Console.WriteLine($"    - {Path.GetFileName(stDevDatasetFilename)}.");

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"Completed with {numberOfRows} rows, elapsed: {timer.Elapsed.TotalSeconds} seconds.\n");
            Console.ResetColor();
            return numberOfRows;
        }

        private Dictionary<string, Dictionary<string, SummationValue>> ProcessBatchResultsFile(int batchNumber, FileInfo batchFile)
        {
            var runShift = BatchRuns * (batchNumber - 1);
            var dataset = new Dictionary<string, Dictionary<string, SummationValue>>();

            using var sr = batchFile.OpenText();
            var line = sr.ReadLine();
            if (line == null)
            {
                Console.WriteLine("[FAILED] Empty batch results dataset file.");
                return dataset;
            }

            var headers = line.Split(Delimeter);
            if (headers.Length > identifiers.Count)
            {
                var keys = identifiers.Keys;
                foreach (var item in keys)
                {
                    identifiers[item] = Array.FindIndex(headers, s => s.Equals(item, StringComparison.OrdinalIgnoreCase));
                    if (identifiers[item] < 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("[FAILED]");
                        Console.WriteLine($"\tInvalid batch result dataset file format, missing identifier: {item}.");
                        Console.ResetColor();
                        return dataset;
                    }
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[FAILED]");
                Console.WriteLine("\tInvalid batch result dataset file format, must have unique identifiers before variables.");
                Console.ResetColor();
                return dataset;
            }

            var starIndex = identifiers["gender"] + 1;
            if (factors.Count < 1)
            {
                for (var idx = starIndex; idx < headers.Length; idx++)
                {
                    factors.Add(headers[idx].Trim());
                }
            }

            while ((line = sr.ReadLine()) != null)
            {
                var fields = line.Split(Delimeter);
                var runNumber = runShift + int.Parse(fields[identifiers["run"]]);
                var index = $"{fields[identifiers["source"]]}_{runNumber}_{fields[identifiers["time"]]}";
                var rowAge = int.Parse(fields[identifiers["index_id"]]);
                if (!AgeRange.Contains(rowAge))
                {
                    continue;
                }

                if (!dataset.TryGetValue(index, out var factorDataset))
                {
                    factorDataset = new Dictionary<string, SummationValue>(StringComparer.OrdinalIgnoreCase);
                    dataset.Add(index, factorDataset);
                    foreach (var factor in factors)
                    {
                        factorDataset.Add(factor, new SummationValue
                        {
                            Count = 0,
                            Sum = 0.0,
                            SumSq = 0.0,
                        });
                    }
                }

                var count = int.Parse(fields[identifiers["count"]]);
                for (var idx = starIndex; idx < headers.Length; idx++)
                {
                    var factor = headers[idx].Trim();
                    if (!factors.Contains(factor))
                    {
                        continue;
                    }

                    var metric = count * double.Parse(fields[idx].Trim());
                    factorDataset[factor].Add(count, metric);
                }
            }

            return dataset;
        }
    }
}