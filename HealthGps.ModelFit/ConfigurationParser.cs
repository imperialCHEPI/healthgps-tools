using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

using Microsoft.Data.Analysis;

namespace HealthGps.ModelFit
{
    public static class ConfigurationParser
    {
        public static RiskFactorModelDefinition LoadRiskFactorModel(FileInfo filename)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            Dto.RiskFactorModelDto dto;
            using (var reader = new StreamReader(filename.FullName))
            {
                dto = JsonSerializer.Deserialize<Dto.RiskFactorModelDto>(reader.ReadToEnd(), options);
            }

            if (dto == null)
            {
                throw new InvalidDataException($"Failed to parse configuration file: {filename.FullName}");
            }

            var rootFolder = filename.Directory.FullName;
            var datasetFilename = new FileInfo(Path.Combine(rootFolder, dto.Dataset.Filename));
            if (!datasetFilename.Exists)
            {
                throw new FileNotFoundException($"Model dataset file: {filename.FullName} not found.");
            }

            var dataset = LoadDataset(datasetFilename, dto.Dataset.Format, dto.Dataset.Delimiter);
            var riskFactors = new Dictionary<string, int>(
                dto.Modelling.RiskFactors, StringComparer.OrdinalIgnoreCase);

            // Quick sanity check
            var dynamicRiskFactor = dto.Modelling.DynamicRiskFactor.Trim();
            if (!string.IsNullOrWhiteSpace(dynamicRiskFactor) && !riskFactors.ContainsKey(dynamicRiskFactor))
            {
                throw new InvalidDataException(
                    $"Invalid configuration, 'dynamic factor: {dynamicRiskFactor}' not found in risk factors.");
            }

            foreach (var col in riskFactors)
            {
                if (dataset.Columns.IndexOf(col.Key) < 0)
                {
                    throw new InvalidDataException(
                        $"Invalid configuration, 'factor: {col.Key}' not found in dataset.");
                }
            }

            var modelTypes = new Dictionary<string, RiskFactorModelType>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in dto.Modelling.Models)
            {
                var dynamicFactor = item.Value.IncludeDynamicRiskFactor;
                var modelFilename = new FileInfo(Path.Combine(rootFolder, item.Value.Filename));
                modelTypes.Add(item.Key, new RiskFactorModelType(item.Key, modelFilename, dynamicFactor));
            }

            return new RiskFactorModelDefinition(dataset, riskFactors, modelTypes, dynamicRiskFactor);
        }

        private static DataFrame LoadDataset(FileInfo filename, string format, string delimiter)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Loading input dataset:");
            Console.ResetColor();
            Console.WriteLine(filename.FullName);
            if (!string.Equals("CSV", format, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Dataset file format: {format} is not supported.");
            }

            var dataTypes = CreateDoubleColumnsKind(filename);
            return LoadCsv(filename, delimiter[0], true, dataTypes);
        }

        private static Type[] CreateDoubleColumnsKind(FileInfo sourceFile)
        {
            Type[] columns = null;
            using (var reader = new StreamReader(sourceFile.FullName))
            {
                var line = reader.ReadLine();
                var fields = line.Split(',');
                columns = new Type[fields.Length];
                for (int i = 0; i < fields.Length; i++)
                {
                    columns[i] = typeof(double);
                }
            }

            return columns;
        }

        public static DataFrame LoadCsv(FileInfo fileInfo, char separator = ',',
            bool header = true, Type[] dataTypes = null, long numberOfRowsToRead = -1)
        {
            return DataFrame.LoadCsv(
                filename: fileInfo.FullName,
                separator: separator,
                header: header,
                dataTypes: dataTypes,
                numRows: (int)numberOfRowsToRead);
        }

        public static void WriteRiskFactorToJson(DirectoryInfo outputFolder, RiskFactorModel model)
        {
            if (!outputFolder.Exists)
            {
                Directory.CreateDirectory(outputFolder.FullName);
            }

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters =
                {
                    new Array2dJsonConverter(),
                }
            };

            foreach (var item in model.Models)
            {
                var fileInfo = model.Definition.ModelTypes[item.Key].Filename;
                var modelFilename = Path.Combine(outputFolder.FullName, fileInfo.Name);
                using (var createStream = File.Create(modelFilename))
                {
                    JsonSerializer.SerializeAsync(createStream, item.Value, options).Wait();
                }
            }
        }
    }
}