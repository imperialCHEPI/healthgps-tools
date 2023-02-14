using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace HealthGPS.Tools.Output;

public class RuntimeLogsDatasetBuilder
{
    private readonly DirectoryInfo sourceFolder;
    private readonly int? maxLogFiles;

    public RuntimeLogsDatasetBuilder(OutputToolOptions config)
    {
        sourceFolder = new DirectoryInfo(Path.Combine(config.SourceFolder.FullName, config.RuntimeLogs.SourceSubFolder));
        maxLogFiles = config.MaxResultFiles;

        Logger = new StringWriter();
        if (!sourceFolder.Exists)
        {
            Logger.WriteLine();
            Logger.WriteLineWithColor($"Simulation logs folder: {sourceFolder.FullName} not found.\n", null, ConsoleColor.Red);
        }
    }

    public TextWriter Logger { get; }

    public IReadOnlyCollection<RuntimeLog> Build()
    {
        var dataset = new List<RuntimeLog>();
        if (!sourceFolder.Exists)
        {
            return dataset;
        }

        var timer = new Stopwatch();
        timer.Start();
        var logFiles = sourceFolder.GetFiles("*.txt");
        Logger.WriteLine();
        Logger.WriteLineWithColor($"Processing Simulation Logs: {logFiles.Length}\n", null, ConsoleColor.Cyan);
        var orderedBacthFiles = new SortedDictionary<int, FileInfo>();
        foreach (var file in logFiles)
        {
            var batch_idx = GetIntFromEndOfString(Path.GetFileNameWithoutExtension(file.Name));
            if (batch_idx < 1)
            {
                Logger.WriteLine($"Invalid batch file name: {file.Name}, missing index");
                continue;
            }

            orderedBacthFiles.Add(batch_idx, file);
        }

        if (maxLogFiles.HasValue && maxLogFiles.Value > 0 && maxLogFiles.Value < orderedBacthFiles.Count)
        {
            orderedBacthFiles = new SortedDictionary<int, FileInfo>(
                orderedBacthFiles.Take(maxLogFiles.Value).ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        }

        dataset = ParseLogFiles(orderedBacthFiles);

        timer.Stop();
        Logger.WriteLine();
        Logger.WriteLineWithColor($"Complete in {timer.Elapsed.TotalSeconds} seconds", null, ConsoleColor.White);
        return dataset;
    }

    private List<RuntimeLog> ParseLogFiles(IReadOnlyDictionary<int, FileInfo> logFiles)
    {
        const string timeOfDayToken = "Today:";
        const string runnerToken = "_runner";
        const string scenarioEnd = "ended";
        const string runnerFinish = "finished ";

        var results = new List<RuntimeLog>();
        var fileIdPad = logFiles.Count.ToString().Length + 2;
        var fileNamePad = 0;
        if (logFiles.Count > 0)
        {
            fileNamePad = logFiles.First().Value.Name.Length + fileIdPad;
        }

        var stringFormat = string.Format("{{0,{0}}} - {{1,-{1}}} ...", fileIdPad, fileNamePad);
        foreach (var file in logFiles)
        {
            Logger.Write(stringFormat, file.Key, file.Value.Name);
            using (var sr = file.Value.OpenText())
            {
                var line = string.Empty;
                var timeOfDay = DateTime.MinValue;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Contains(timeOfDayToken))
                    {
                        timeOfDay = ParseTimeOfDay(line);
                        break;
                    }
                }

                if (timeOfDay == DateTime.MinValue)
                {
                    Logger.WriteLine(" [FAILED] missing time of day.");
                    continue;
                }

                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Contains(runnerToken, StringComparison.OrdinalIgnoreCase))
                    {
                        var isScenarioEnd = line.Contains(scenarioEnd, StringComparison.OrdinalIgnoreCase);
                        if (isScenarioEnd || line.Contains(runnerFinish, StringComparison.OrdinalIgnoreCase))
                        {
                            var source = ParseSourceScenario(line);
                            var elapsedTime = ParseElapsedTime(line);
                            var currentRun = ParseScenarioRun(line, file.Key);
                            results.Add(new RuntimeLog
                            {
                                JobId = file.Key,
                                Source = source,
                                Run = currentRun,
                                TimeOfDay = timeOfDay,
                                ElapsedMs = elapsedTime
                            });
                        }
                    }
                }
            }

            Logger.WriteLine(" [OK]");
        }

        return results;
    }

    private static DateTime ParseTimeOfDay(string line)
    {
        var fields = line.Split(' ');
        if (fields.Length < 3)
        {
            throw new InvalidCastException($"Failed to parse time of day from line: {line}, not enough fields.");
        }

        return DateTime.Parse(string.Join(" ", fields, 1, 2));
    }

    private static double ParseElapsedTime(string line)
    {
        var fields = line.Split("in ");
        if (fields.Length < 2)
        {
            throw new InvalidCastException($"Failed to parse elapsed time from line: {line}, not enough fields.");
        }

        return GetDoubleFromStartOfString(fields[1].Trim());
    }

    private static string ParseSourceScenario(string line)
    {
        var fields = line.Split(':');
        if (fields.Length < 2)
        {
            throw new InvalidCastException($"Failed to parse source scenario from line: {line}, not enough fields.");
        }

        fields = fields[1].Split(',');
        if (fields.Length < 2)
        {
            throw new InvalidCastException($"Failed to parse source scenario from line: {line}, missing fields.");
        }

        var scenarioIdx = fields[0].IndexOf("-");
        if (scenarioIdx > 0)
        {
            return fields[0][(scenarioIdx + 1)..].Trim();
        }

        var experiment = "experiment";
        if (fields[1].Contains(experiment, StringComparison.OrdinalIgnoreCase))
        {
            return experiment;
        }

        return "unknown";
    }

    private static int ParseScenarioRun(string line, int experimentNumber)
    {
        var fields = line.Split('#');
        if (fields.Length == 2)
        {
            var runIdx = fields[1].IndexOf("ended", StringComparison.OrdinalIgnoreCase);
            if (runIdx > 0)
            {
                return int.Parse(fields[1][..runIdx]);
            }
        }

        return experimentNumber;
    }

    private static int GetIntFromEndOfString(string text)
    {
        if (text.EndsWith(".pbs", StringComparison.OrdinalIgnoreCase))
        {
            // Old output filename format: XXX_N.pbs.txt
            var index = text.IndexOf(".pbs");
            if (index > 0)
            {
                text = text[..index];
            }

            index = text.Length - 1;
            while (index >= 0)
            {
                if (!char.IsNumber(text[index])) break;
                index--;
            }

            if (int.TryParse(text.AsSpan(index + 1), out int number))
            {
                return number;
            }
        }
        else
        {
            // Standard output filename format: XXX.pbs.N.txt
            return FileHelper.GetIntFromEndOfString(text);
        }

        return -1;
    }

    private static double GetDoubleFromStartOfString(string text)
    {
        int i = text.Length - 1;
        while (i >= 0)
        {
            if (char.IsNumber(text[i]))
            {
                break;
            }

            i--;
        }

        if (double.TryParse(text.AsSpan(0, i + 1), out double number))
        {
            return number;
        }

        return double.NaN;
    }
}
