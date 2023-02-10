using HealthGPS.Tools.Output;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace HealthGPS.Tools;

public class OutputTool : IHealthGpsTool
{
    private readonly OutputToolOptions config;

    public OutputTool(FileInfo configFile)
    {
        Name = "Output Tool";
        Logger = new StringWriter();

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(configFile.FullName, optional: false);

        var settings = builder.Build();
        config = CreateConfiguration(settings);
    }

    public string Name { get; }

    public TextWriter Logger { get; }

    public void Execute()
    {
        var workers = new List<Task<TextWriter>>();
        if (config.RuntimeLogs != null && config.RuntimeLogs.IsActive)
        {
            var logsBuilder = new RuntimeLogsDatasetBuilder(config);
            WriteQueueMessage("Queuing runtime logs dataset builder job ...");
            workers.Add(Task<TextWriter>.Factory.StartNew(() =>
            {
                var logsDataset = logsBuilder.Build();
                var outputFile = new FileInfo(Path.Combine(config.OutputFolder.FullName, config.RuntimeLogs.OutputFilename));
                WriteRuntimeLogsToCsv(outputFile, logsDataset);
                return logsBuilder.Logger;
            }, TaskCreationOptions.LongRunning));
        }

        var aggregator = new SimulationResultsAggregator(config);
        var totalRuns = aggregator.CreateGlobalSummary();

        WriteQueueMessage("Queuing simulation batch results processing job ...");
        workers.Add(Task<TextWriter>.Factory.StartNew(() =>
        {
            var accumulators = CreateAccumulators(config);
            var logger = aggregator.Execute(accumulators);

            WriteResultsToCsv(logger, config, accumulators, totalRuns);
            return logger;
        }, TaskCreationOptions.LongRunning));

        if (config.BaselineDataset != null && config.BaselineDataset.IsActive)
        {
            var baselineBuilder = new BaselineDatasetBuilder(config, totalRuns);
            WriteQueueMessage("Queuing baseline dataset builder job ...");
            workers.Add(Task<TextWriter>.Factory.StartNew(() =>
            {
                var summary = baselineBuilder.Build();
                WriteSummaryToJson(baselineBuilder.Logger, config, summary, totalRuns);
                return baselineBuilder.Logger;
            }, TaskCreationOptions.LongRunning));
        }

        if (config.HCEDataset != null && config.HCEDataset.IsActive)
        {
            var hceBuilder = new HCEDatasetBuilder(config, totalRuns);
            var hceDataset = hceBuilder.Build();
        }

        Console.WriteLine($"Waiting for {workers.Count} queued jobs to complete.");
        Task.WaitAll(workers.ToArray());
        foreach (var worker in workers)
        {
            Logger.WriteLine(worker.Result);
        }
    }

    private static void WriteQueueMessage(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine(message);
        Console.WriteLine();
        Console.ResetColor();
    }

    private static OutputToolOptions CreateConfiguration(IConfigurationRoot settings)
    {
        var optionsSection = settings.GetSection("OutputOptions");
        if (optionsSection == null)
        {
            throw new InvalidDataException("Invalid configuration v3 format, missing: OutputOptions section.");
        }

        var options = optionsSection.Get<Dto.OutputToolOptionsDto>();

        var baseline = options.BaselineDataset;
        var logs = options.RuntimeLogs;
        var healthcost = options.HCEDataset;
        ValidateFactorsFilter(baseline.FactorsFilter);
        ValidateFactorsFilter(healthcost.FactorsFilter);
        var config = new OutputToolOptions
        {
            Age = new Interval(options.Age.Start, options.Age.Finish),
            Run = new Interval(options.Run.Start, options.Run.Finish),
            Year = new Interval(options.Year.Start, options.Year.Finish),
            Delimiter = options.Delimiter,
            OutputFilename = options.OutputFilename,
            MaxResultFiles = options.MaxResultFiles,
            BaselineDataset = new DatasetOptions
            {
                IsActive = baseline.IsActive,
                OutputFilename = baseline.OutputFilename,
                FactorsFilter = baseline.FactorsFilter,
            },
            RuntimeLogs = new RuntimeLogs
            {
                IsActive = logs.IsActive,
                SourceSubFolder = logs.SourceSubFolder,
                OutputFilename = logs.OutputFilename,
            },
            HCEDataset = new DatasetOptions
            {
                IsActive = healthcost.IsActive,
                OutputFilename = healthcost.OutputFilename,
                FactorsFilter = healthcost.FactorsFilter,
            },
        };

        IConfiguration osSection;
        if (OperatingSystem.IsWindows())
        {
            osSection = settings.GetSection("Platform").GetSection("Windows");
        }
        else if (OperatingSystem.IsLinux())
        {
            osSection = settings.GetSection("Platform").GetSection("Unix");
        }
        else
        {
            throw new InvalidOperationException($"Unsupported OS platform: {Environment.OSVersion}");
        }

        config.SourceFolder = new DirectoryInfo(osSection.GetSection("SourceFolder").Get<string>());
        config.OutputFolder = new DirectoryInfo(osSection.GetSection("OutputFolder").Get<string>());

        return config;
    }

    private static void ValidateFactorsFilter(List<string> factorsFilter)
    {
        if (factorsFilter == null || factorsFilter.Count < 2)
        {
            return;
        }

        var query = factorsFilter.GroupBy(x => x)
          .Where(g => g.Count() > 1)
          .Select(y => y.Key)
          .ToList();

        if (query.Any())
        {
            throw new InvalidDataException($"Factors filter list must not contain duplicate: {string.Join(",", query)}");
        }
    }

    private static Dictionary<Gender, List<IMeasureAccumulator>> CreateAccumulators(OutputToolOptions config)
    {
        var accumulators = new Dictionary<Gender, List<IMeasureAccumulator>>();
        foreach (var gender in Enum.GetNames(typeof(Gender)))
        {
            var genderType = (Gender)Enum.Parse(typeof(Gender), gender);
            accumulators.Add(genderType, new List<IMeasureAccumulator>()
                {
                    new BaselineAccumulator(genderType, config.Year.Start, config.Year.Finish),
                    new InterventionAccumulator(genderType, config.Year.Start, config.Year.Finish),
                    new DifferenceAccumulator(genderType, config.Year.Start, config.Year.Finish),
                    new PercentDifferenceAccumulator(genderType, config.Year.Start, config.Year.Finish)
                });
        }

        return accumulators;
    }

    private static void WriteResultsToCsv(
        TextWriter logger,
        OutputToolOptions config,
        IReadOnlyDictionary<Gender, List<IMeasureAccumulator>> accumulators,
        int totalRuns)
    {
        logger.WriteLine($"  + Outputs: {config.OutputFolder}");
        if (accumulators.Count != 2)
        {
            logger.WriteLineWithColor(
                $"Failed, number of gender entries mismatch: {accumulators.Count} vs. 2",
                null,
                ConsoleColor.Red);
            return;
        }

        var males = accumulators[Gender.Male];
        foreach (var maleMeaseure in males)
        {
            if (maleMeaseure.IsAddAllowed)
            {
                maleMeaseure.CalculateAverages(totalRuns);
            }

            var femaleMeasure = accumulators[Gender.Female].FirstOrDefault(s => s.Measure == maleMeaseure.Measure);
            if (femaleMeasure != null)
            {
                if (femaleMeasure.IsAddAllowed)
                {
                    femaleMeasure.CalculateAverages(totalRuns);
                }

                WriteMeasureToCsv(logger, config, maleMeaseure, femaleMeasure, totalRuns);
            }
            else
            {
                logger.WriteLineWithColor(
                    $"Missing female accumulator for measure type: {maleMeaseure.Measure}.",
                    null,
                    ConsoleColor.DarkRed);
            }
        }
    }

    private static void WriteMeasureToCsv(
        TextWriter logger,
        OutputToolOptions config,
        IMeasureAccumulator maleMeaseure,
        IMeasureAccumulator femeMeaseure,
        int totalRuns)
    {
        var meanFilename = Path.Combine(
            config.OutputFolder.FullName,
            config.CreateMeanFilename(maleMeaseure.Measure, totalRuns));

        var stDevFilename = Path.Combine(
            config.OutputFolder.FullName,
            config.CreateStDevFilename(maleMeaseure.Measure, totalRuns));

        logger.WriteLine($"    - {Path.GetFileName(meanFilename)}");
        logger.WriteLine($"    - {Path.GetFileName(stDevFilename)}");
        using var swMean = new StreamWriter(meanFilename);
        using var swStDev = new StreamWriter(stDevFilename);

        swMean.Write("Year");
        swStDev.Write("Year");
        foreach (string factor in maleMeaseure.Factors)
        {
            swMean.Write($",{factor}");
            swStDev.Write($",{factor}");
        }

        // Write males data
        swMean.WriteLine();
        swStDev.WriteLine();
        for (int y = config.Year.Start; y <= config.Year.Finish; y++)
        {
            swMean.Write($"{y}");
            swStDev.Write($"{y}");
            foreach (string factor in maleMeaseure.Factors)
            {
                if (factor.Equals("gender", StringComparison.OrdinalIgnoreCase))
                {
                    swMean.Write($",{Gender.Male}");
                    swStDev.Write($",{Gender.Male}");
                }
                else
                {
                    var factorValue = maleMeaseure[factor, y];
                    swMean.Write($",{factorValue.Mean:G17}");
                    swStDev.Write($",{factorValue.StDev:G17}");
                }
            }

            swMean.WriteLine();
            swStDev.WriteLine();
        }

        // Write females data
        for (int y = config.Year.Start; y <= config.Year.Finish; y++)
        {
            swMean.Write($"{y}");
            swStDev.Write($"{y}");
            foreach (string factor in femeMeaseure.Factors)
            {
                if (factor.Equals("gender", StringComparison.OrdinalIgnoreCase))
                {
                    swMean.Write($",{Gender.Female}");
                    swStDev.Write($",{Gender.Female}");
                }
                else
                {
                    var factorValue = femeMeaseure[factor, y];
                    swMean.Write($",{factorValue.Mean:G17}");
                    swStDev.Write($",{factorValue.StDev:G17}");
                }
            }

            swMean.WriteLine();
            swStDev.WriteLine();
        }
    }

    private static void WriteRuntimeLogsToCsv(FileInfo outputFile, IReadOnlyCollection<RuntimeLog> dataset)
    {
        using var sw = new StreamWriter(outputFile.FullName);
        sw.WriteLine(RuntimeLog.CsvHeader());
        foreach (var element in dataset)
        {
            var elapsed = TimeSpan.FromMilliseconds(element.ElapsedMs);
            sw.WriteLine("{0},{1},{2}", element.ToString(), elapsed.TotalMinutes, elapsed.TotalHours);
        }
    }

    private static void WriteSummaryToJson(
        TextWriter logger,
        OutputToolOptions config,
        IReadOnlyDictionary<string, FactorMoment> factors,
        int totalRuns)
    {
        var datasetFilename = config.BaselineDataset.OutputFilename;
        datasetFilename = datasetFilename.Replace("{RUNS}", totalRuns.ToString(), true, null);
        var ext = Path.GetExtension(datasetFilename);
        var summaryFilename = datasetFilename.Replace(ext, "_summary.json", StringComparison.OrdinalIgnoreCase);
        summaryFilename = Path.Combine(config.OutputFolder.FullName, summaryFilename);

        var dataset = new
        {
            Name = "Baseline dataset",
            Age = factors.First().Value.Males.Select(s => s.Key).ToList(),
            Factors = factors
        };

        using FileStream createStream = File.Create(summaryFilename);
        JsonSerializerOptions options = new()
        {
            Converters =
            {
                new FactorMomentJsonConverter(),
                new RealNumberJsonConverter()
            },
            WriteIndented = false,
        };

        JsonSerializer.SerializeAsync(createStream, dataset, options).Wait();

        logger.WriteLine($"  + Output: {summaryFilename}");
    }
}
