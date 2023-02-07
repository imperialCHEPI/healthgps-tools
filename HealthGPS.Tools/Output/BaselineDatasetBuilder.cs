using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace HealthGPS.Tools.Output;

public class BaselineDatasetBuilder
{
    private static readonly Dictionary<string, int> identifiers = new()
    {
        {"gender_name", -1 },
        {"index_id", -1 },
        {"gender", -1 }
    };

    public BaselineDatasetBuilder(OutputToolOptions config, int totalRuns)
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
        StartTimePattern = $"{Delimeter}{config.Year.Start}{Delimeter}";
        SourceFolder = config.SourceFolder;
        OutputFolder = config.OutputFolder;
        Options = config.BaselineDataset;
        MaxResultFiles = config.MaxResultFiles;
        TotalRuns = totalRuns;
        Logger = new StringWriter();
    }


    public string Delimeter { get; }

    public string StartTimePattern { get; }

    public DirectoryInfo SourceFolder { get; }

    public DirectoryInfo OutputFolder { get; }

    public DatasetOptions Options { get; }

    public int? MaxResultFiles { get; }

    public int TotalRuns { get; }

    public TextWriter Logger { get; }

    public IReadOnlyDictionary<string, FactorMoment> Build()
    {
        if (Options == null)
        {
            Logger.WriteLine("No baseline dataset options, no action.");
            return new Dictionary<string, FactorMoment>();
        }

        var outputFilename = Options.OutputFilename.Replace("{RUNS}", TotalRuns.ToString(), true, null);
        var datasetFilename = Path.Combine(OutputFolder.FullName, outputFilename);
        var orderedBacthFiles = FileHelper.GetBatchResultFiles(SourceFolder, "*.csv", MaxResultFiles);

        CreateDatasetFile(orderedBacthFiles, datasetFilename);

        return SummariseBaselineFile(datasetFilename);
    }

    private void CreateDatasetFile(IReadOnlyDictionary<int, FileInfo> batchResultFiles, string baselienFilename)
    {
        Logger.WriteLineWithColor($"Creating baseline dataset from {batchResultFiles.Count} files.", null, ConsoleColor.Cyan);

        var timer = new Stopwatch();
        timer.Start();
        var needHeader = true;
        using var sw = new StreamWriter(baselienFilename);
        var numberOfRows = 0;
        foreach (var file in batchResultFiles)
        {
            Logger.Write("  - File: {0} ... ", file.Value.FullName);
            using var sr = file.Value.OpenText();
            var line = sr.ReadLine();
            if (needHeader)
            {
                sw.WriteLine($"job_id{Delimeter}{line}");
                needHeader = false;
            }

            var fileRows = 0;
            while ((line = sr.ReadLine()) != null)
            {
                if (line.Contains("baseline", StringComparison.OrdinalIgnoreCase) &&
                    line.Contains(StartTimePattern))
                {
                    fileRows++;
                    sw.WriteLine($"{file.Key}{Delimeter}{line}");
                }
            }

            numberOfRows += fileRows;
            Logger.WriteLine($"[OK], rows: {fileRows}");
        }

        timer.Stop();
        Logger.WriteLine();
        Logger.WriteLine($"  + Output: {baselienFilename}.");

        Logger.WriteLine();
        Logger.WriteLineWithColor(
            $"Completed with {numberOfRows} rows, elapsed: {timer.Elapsed.TotalSeconds} seconds.\n",
            null,
            ConsoleColor.White);
        sw.Close();
    }

    private IReadOnlyDictionary<string, FactorMoment> SummariseBaselineFile(string baselienFilename)
    {
        Logger.WriteLine();
        Logger.WriteWithColor("Creating baseline dataset statistical summary ... ", null, ConsoleColor.Cyan);

        var timer = new Stopwatch();
        timer.Start();
        var factors = new Dictionary<string, FactorMoment>();
        using var sr = new StreamReader(baselienFilename);
        var line = sr.ReadLine();
        if (line == null)
        {
            Logger.WriteLineWithColor("[FAILED] Empty baseline dataset file.", null, ConsoleColor.Red);
            return factors;
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
                    Logger.WriteLineWithColor("[FAILED]", null, ConsoleColor.Red);
                    Logger.WriteLineWithColor(
                        $"\tInvalid baseline dataset file format, missing identifier: {item}.",
                        null,
                        ConsoleColor.Red);
                    return factors;
                }
            }
        }
        else
        {
            Logger.WriteLineWithColor("[FAILED]", null, ConsoleColor.Red);
            Logger.WriteLineWithColor(
                "\tInvalid baseline dataset file format, must have unique identifiers before variables.",
                null,
                ConsoleColor.Red);
            return factors;
        }

        var starIndex = identifiers["gender"] + 1;
        for (int index = starIndex; index < headers.Length; index++)
        {
            var factorKey = headers[index].Trim();
            factors.Add(factorKey, new FactorMoment(factorKey));
        }

        while ((line = sr.ReadLine()) != null)
        {
            var fields = line.Split(Delimeter);
            var rowGender = (Gender)Enum.Parse(typeof(Gender), fields[identifiers["gender_name"]].Trim(), true);
            var rowAge = int.Parse(fields[identifiers["index_id"]]);
            for (int index = starIndex; index < fields.Length; index++)
            {
                var factor = factors[headers[index].Trim()];
                var factorValue = double.Parse(fields[index].Trim());
                factor.Add(rowGender, rowAge, factorValue);
            }
        }

        Logger.WriteLineWithColor("[OK]", null, ConsoleColor.Green);

        timer.Stop();
        Logger.WriteLine();
        Logger.WriteLineWithColor(
            $"Completed, elapsed: {timer.Elapsed.TotalSeconds} seconds.\n",
            null,
            ConsoleColor.White);
        return factors;
    }
}