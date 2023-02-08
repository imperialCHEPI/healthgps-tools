using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace HealthGPS.Tools.Output;

public class SimulationResultsAggregator
{
    private readonly object syncLock;
    private readonly SortedDictionary<int, FileInfo> jsonFiles;
    private readonly SortedDictionary<int, FileInfo> dataFiles;
    private readonly OutputToolOptions config;

    public SimulationResultsAggregator(OutputToolOptions options)
    {
        if (!options.SourceFolder.Exists)
        {
            throw new ArgumentException($"Source directory: {options.SourceFolder.FullName} not found.");
        }

        if (!options.OutputFolder.Exists)
        {
            var info = Directory.CreateDirectory(options.OutputFolder.FullName);
            if (!info.Exists)
            {
                throw new DirectoryNotFoundException($"Output folder: {options.OutputFolder.FullName} not found.");
            }
        }

        syncLock = new object();
        config = options;
        jsonFiles = FileHelper.GetBatchResultFiles(options.SourceFolder, "*.json", options.MaxResultFiles);
        dataFiles = FileHelper.GetBatchResultFiles(options.SourceFolder, "*.csv", options.MaxResultFiles);
        ValidateSourceFiles();
        GlobalSummaryTotalRuns = -1;
    }

    public int GlobalSummaryTotalRuns { get; private set; }

    public TextWriter Execute(Dictionary<Gender, List<IMeasureAccumulator>> accumulators)
    {
        if (GlobalSummaryTotalRuns < 0)
        {
            throw new InvalidOperationException("The global summary must be created before processing batch files.");
        }

        lock (syncLock)
        {
            var logger = new StringWriter();

            // Reset accumulators
            foreach (var gender in accumulators)
            {
                foreach (var measeure in gender.Value)
                {
                    measeure.Reset();
                }
            }

            ProcessResultFiles(logger, accumulators);
            return logger;
        }
    }

    public int CreateGlobalSummary()
    {
        lock (syncLock)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Processing {jsonFiles.Count} simulation global result files.");
            Console.ResetColor();

            var timer = new Stopwatch();
            timer.Start();
            var outputSummaryFile = Path.Combine(config.OutputFolder.FullName, config.OutputFilename);
            GlobalSummaryTotalRuns = CreateGlobalSummary(outputSummaryFile);
            timer.Stop();
            var summaryElapsed = timer.Elapsed;

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"Completed with {GlobalSummaryTotalRuns} runs, elapsed: {summaryElapsed.TotalSeconds} seconds.\n");
            Console.ResetColor();
        }

        return GlobalSummaryTotalRuns;
    }

    private int ProcessResultFiles(TextWriter logger, Dictionary<Gender, List<IMeasureAccumulator>> accumulators)
    {
        logger.WriteLineWithColor($"Processing {dataFiles.Count} simulation batch result files.", null, ConsoleColor.Cyan);

        var timer = new Stopwatch();
        timer.Start();
        var dataTotalRuns = ProcessBatchResultDataset(logger, accumulators);
        timer.Stop();
        var dataElapsed = timer.Elapsed;
        logger.WriteLineWithColor(
            $"\nCompleted with {dataTotalRuns} runs, elapsed: {dataElapsed.TotalSeconds} seconds.\n",
            null,
            ConsoleColor.White);

        if (GlobalSummaryTotalRuns != dataTotalRuns)
        {
            logger.WriteLineWithColor(
                $"Global summary and results data number of runs mismatch: {GlobalSummaryTotalRuns} vs. {dataTotalRuns}.",
                null,
                ConsoleColor.DarkRed);
        }

        return dataTotalRuns;
    }

    private void ValidateSourceFiles()
    {
        if (jsonFiles.Count != dataFiles.Count)
        {
            throw new InvalidDataException(
                $"Number of batch summary and result files mismatch {jsonFiles.Count} vs. {dataFiles.Count}.");
        }

        var errorLog = new List<int>();
        foreach (var file in jsonFiles)
        {
            if (!dataFiles.ContainsKey(file.Key))
            {
                errorLog.Add(file.Key);
            }
        }

        if (errorLog.Count > 0)
        {
            throw new InvalidDataException($"Missing data file for job ids: {string.Join(", ", errorLog)}.");
        }
    }

    private int CreateGlobalSummary(string outputFile)
    {
        var totalRuns = 0;
        JsonNode output = null;
        var policy = string.Empty;
        var numberOfDiseases = 0;
        var isFirstBatchFile = true;
        foreach (var file in jsonFiles)
        {
            Console.Write("  - File: {0} ... ", file.Value);
            using var sr = new FileStream(file.Value.FullName, FileMode.Open);
            if (isFirstBatchFile)
            {
                var doc = JsonDocument.ParseAsync(sr).Result;
                var array = doc.RootElement.GetProperty("result").EnumerateArray();
                totalRuns = array.Max(s => s.GetProperty("run").GetInt32());
                if (array.Any())
                {
                    var firstRow = array.First();
                    numberOfDiseases = firstRow.GetProperty("disease_prevalence").EnumerateObject().Count();
                }

                policy = doc.RootElement.GetProperty("experiment").GetProperty("intervention").GetString();
                if (policy.Length > 0)
                {
                    policy = string.Concat(policy[0].ToString().ToUpper(), policy.AsSpan(1));
                }

                using var ms = new MemoryStream();
                using (var writer = new Utf8JsonWriter(ms))
                {
                    doc.WriteTo(writer);
                }

                ms.Seek(0, SeekOrigin.Begin);
                output = JsonNode.Parse(ms);

                isFirstBatchFile = false;
                Console.WriteLine($"[OK], runs: {totalRuns}.");
                continue;
            }

            var node = JsonNode.Parse(sr);
            var results = node["result"].AsArray();
            var outputArray = output["result"].AsArray();
            var maxRun = 0;
            foreach (var entry in results)
            {
                var runObj = entry.AsObject();
                var run = runObj["run"].GetValue<int>();
                runObj["run"] = totalRuns + run;

                outputArray.Add(JsonNode.Parse(runObj.ToJsonString()));
                maxRun = Math.Max(maxRun, run);
            }

            totalRuns += maxRun;
            Console.WriteLine($"[OK], runs: {maxRun}.");
        }

        outputFile = outputFile.Replace("{POLICY}", policy, true, null);
        outputFile = outputFile.Replace("{NUMBER}", numberOfDiseases.ToString(), true, null);
        outputFile = outputFile.Replace("{RUNS}", totalRuns.ToString(), true, null);
        Console.WriteLine("\n  + Output: {0}.\n", outputFile);
        using (var sw = new FileStream(outputFile, FileMode.Create))
        {
            using var writer = new Utf8JsonWriter(sw);
            writer.WriteStartObject();

            var obj = output.AsObject();
            foreach (var item in obj)
            {
                if (item.Key.Equals("result", StringComparison.OrdinalIgnoreCase))
                {
                    writer.WriteStartArray(item.Key);
                    var array = item.Value.AsArray();
                    foreach (var row in array)
                    {
                        row.WriteTo(writer);
                        writer.Flush();
                    }

                    writer.WriteEndArray();
                }
                else
                {
                    writer.WritePropertyName(item.Key);
                    item.Value.WriteTo(writer);
                    writer.Flush();
                }
            }

            writer.WriteEndObject();
        }

        return totalRuns;
    }

    private int ProcessBatchResultDataset(TextWriter logger, Dictionary<Gender, List<IMeasureAccumulator>> accumulators)
    {
        var factors = new List<string>();
        var factorKeys = new List<string> { "source", "run", "time", "gender_name" };

        int ageIndex = factorKeys.Count;
        int numberOfRuns = 0;
        var padding = dataFiles.Max(s => s.Value.FullName.Length) + 1;
        var fileFormat = string.Format("  - File: {{0,-{0}}} ... ", padding);
        foreach (var file in dataFiles)
        {
            logger.Write(fileFormat, file.Value);
            var fileContainsWarning = false;
            var countIndex = LoadResultFileIndex(config, file.Value, factors, factorKeys, ageIndex,
                out Dictionary<SummaryKey, Dictionary<int, List<double>>> baselines,
                out Dictionary<SummaryKey, Dictionary<int, List<double>>> interventions);

            if (countIndex < 0)
            {
                logger.WriteLineWithColor("[FAILED] - missing count column", null, ConsoleColor.Red);
                continue;
            }

            if (baselines.Count != interventions.Count)
            {
                fileContainsWarning = true;
                logger.WriteLineWithColor("[FAILED] - missing count column", null, ConsoleColor.Red);
                logger.WriteLineWithColor(
                    $"[WARNING] - dataset size mismatch: {baselines.Count} vs. {interventions.Count}.",
                    null,
                    ConsoleColor.DarkYellow);
            }

            var baselineMaxRuns = baselines.Keys.ToList().Max(s => s.Run);
            var interventionsMaxRuns = interventions.Keys.ToList().Max(s => s.Run);
            if (baselineMaxRuns != interventionsMaxRuns)
            {
                fileContainsWarning = true;
                logger.WriteLineWithColor(
                    $"[WARNING] - maximum runs mismatch: {baselineMaxRuns} vs. {interventionsMaxRuns}.",
                    null,
                    ConsoleColor.DarkYellow);
            }

            numberOfRuns += baselineMaxRuns;
            foreach (var gender in accumulators)
            {
                var genderAccumulators = gender.Value;
                for (int run = config.Run.Start; run <= config.Run.Finish; run++)
                {
                    for (int year = config.Year.Start; year <= config.Year.Finish; year++)
                    {
                        // Filter by configuration gender
                        var baselineKey = new SummaryKey(ScenarioType.Baseline, run, year, gender.Key);
                        var policyKey = new SummaryKey(ScenarioType.Intervention, run, year, gender.Key);

                        if (!baselines.TryGetValue(baselineKey, out var baselineData))
                        {
                            fileContainsWarning = true;
                            logger.WriteLineWithColor(
                                $"Missing baseline results for run: {run}, year: {year} and gender: {gender.Key}.",
                                null,
                                ConsoleColor.DarkYellow);
                        }

                        if (!interventions.TryGetValue(policyKey, out var interventionData))
                        {
                            fileContainsWarning = true;
                            logger.WriteLineWithColor(
                                $"Missing policy results for run: {run}, year: {year} and gender: {gender.Key}.",
                                null,
                                ConsoleColor.DarkYellow);
                        }

                        if (baselineData == null || interventionData == null)
                        {
                            continue;
                        }

                        for (int factorIndex = 0; factorIndex < factors.Count; factorIndex++)
                        {
                            var factor = factors[factorIndex];

                            var baseSum = 0.0;
                            var baseCount = 0.0;

                            var policySum = 0.0;
                            var policyCount = 0.0;

                            foreach (var age in baselineData.Keys)
                            {
                                if (!config.Age.Contains(age))
                                {
                                    continue;
                                }

                                baseSum += baselineData[age][factorIndex] * baselineData[age][countIndex];
                                baseCount += baselineData[age][countIndex];

                                policySum += interventionData[age][factorIndex] * interventionData[age][countIndex];
                                policyCount += interventionData[age][countIndex];
                            }

                            foreach (var measure in genderAccumulators)
                            {
                                measure.Add(factor, year, baseSum, baseCount, policySum, policyCount);
                            }
                        }
                    }
                }
            }

            if (fileContainsWarning)
            {
                logger.WriteLine($"    [WARNING], runs: {Math.Min(baselineMaxRuns, interventionsMaxRuns)}");
            }
            else
            {
                logger.WriteLine($"[OK], runs: {baselineMaxRuns}");
            }
        }

        return numberOfRuns;
    }

    private static int LoadResultFileIndex(
        OutputToolOptions config,
        FileInfo sourceFileInfo,
        List<string> factors, List<string> factorKeys,
        int ageIndex,
        out Dictionary<SummaryKey, Dictionary<int, List<double>>> baselines,
        out Dictionary<SummaryKey, Dictionary<int, List<double>>> interventions)
    {
        // Split and index file
        var countIndex = -1;
        baselines = new Dictionary<SummaryKey, Dictionary<int, List<double>>>();
        interventions = new Dictionary<SummaryKey, Dictionary<int, List<double>>>();
        using (var sr = sourceFileInfo.OpenText())
        {
            var line = sr.ReadLine();
            if (line == null)
            {
                throw new InvalidOperationException($"The source line: {sourceFileInfo.FullName} must not be empty.");
            }

            string[] fields;
            if (factors.Count < 1)
            {
                fields = line.Split(config.Delimiter);
                foreach (var factor in fields)
                {
                    if (factorKeys.Contains(factor, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    factors.Add(factor.Trim());
                }
            }

            countIndex = factors.IndexOf("count");
            while ((line = sr.ReadLine()) != null)
            {
                fields = line.Split(config.Delimiter);
                var key = SummaryKey.Parse(fields);
                var age = int.Parse(fields[ageIndex].Trim());
                var row = new List<double>(factors.Count);
                for (int index = ageIndex; index < fields.Length; index++)
                {
                    row.Add(double.Parse(fields[index].Trim()));
                }

                if (key.Scenario == ScenarioType.Baseline)
                {
                    if (!baselines.ContainsKey(key))
                    {
                        // TODO: Add to a processing queue and start a new one
                        baselines.Add(key, new Dictionary<int, List<double>>());
                    }

                    baselines[key].Add(age, row);
                }
                else
                {
                    if (!interventions.ContainsKey(key))
                    {
                        // TODO: Add to a processing queue and start a new one
                        interventions.Add(key, new Dictionary<int, List<double>>());
                    }

                    interventions[key].Add(age, row);
                }
            }
        }

        return countIndex;
    }
}