<Query Kind="Program">
  <Output>DataGrids</Output>
  <Namespace>System.Text.Json.Serialization</Namespace>
  <Namespace>System.Text.Json</Namespace>
</Query>

#load "Process_common.linq"

void Main()
{
	Console.WriteLine("Hello, the process IHME types sucessfully compile.");
}

public class DiseaseSettings
{
	[JsonPropertyName("datatype")]
	public string DataType { get; set; }

	[JsonPropertyName("maximum_age")]
	public int MaxAge { get; set; }

	[JsonPropertyName("value_units")]
	public double Units { get; set; }

	[JsonPropertyName("smoothing_times")]
	public int SmoothingTimes { get; set; }

	[JsonPropertyName("root_folder")]
	public string DataRootFolder { get; set; }

	[JsonPropertyName("location_filename")]
	public string LocationFilename { get; set; }

	[JsonPropertyName("diseases_folder")]
	public string DiseasesFolder { get; set; }

	[JsonPropertyName("cancers_folder")]
	public string CancersFolder { get; set; }

	[JsonPropertyName("cancer_years")]
	public int CancerYears { get; set; }

	[JsonPropertyName("datasource_folder")]
	public string SourceFolder { get; set; }

	[JsonPropertyName("datasource_year")]
	public int SourceYear { get; set; }

	[JsonPropertyName("output_folder")]
	public string OutputFoldername { get; set; }

	public double UnitsMultiplier => Units > 0.0 ? 1.0 / this.Units : 1.0;
	public string OutputSubFolder => $"{OutputFoldername}{SourceYear}";
}

private static void ValidateSchemaVersion(JsonDocument doc)
{
	if (doc.RootElement.TryGetProperty("version", out JsonElement version))
	{
		if (version.GetInt32() != 1)
		{
			throw new InvalidDataException($"Disease schema version mismatch: {version}");
		}
	}
	else
	{
		throw new InvalidDataException($"Disease schema mismatch, missing version");
	}
}

private static DiseaseSettings GetDiseaseSettings(JsonDocument doc)
{
	if (doc.RootElement.TryGetProperty("settings", out JsonElement settings))
	{
		var config = settings.Deserialize<DiseaseSettings>();
		if (!string.Equals(config.DataType, "disease", StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException($"Disease data type mismatch: {config.DataType}");
		}

		return config;
	}

	throw new InvalidDataException("Invalid diseases configuration file schema, missing settings.");
}

// -------------------------------------------------------
// Disease source folder with measure override
// -------------------------------------------------------
struct DiseaseFolder
{
	public DiseaseFolder(DirectoryInfo source) : this(source, null, null)
	{
	}

	public DiseaseFolder(DirectoryInfo source, string outputName) : this(source, "*", outputName)
	{
	}

	public DiseaseFolder(DirectoryInfo source, string sourceName, string outputName)
	{
		if (!source.Exists)
		{
			throw new ArgumentException($"Source data folder: {source.FullName} not found.");
		}

		Info = source;
		SourceMeasure = sourceName;
		OutputMeasure = outputName;
		IsOverridingMeasure = false;
		IsAnySourceMeasure = false;
		if (!string.IsNullOrWhiteSpace(sourceName))
		{
			if (sourceName.Length == 1)
			{
				if (!string.Equals("*", sourceName))
				{
					throw new ArgumentException($"Invalid overriding source wildcard: {sourceName}"); 	
				}
				
				IsAnySourceMeasure = true;
			}
			
			if (string.IsNullOrWhiteSpace(OutputMeasure))
			{
				throw new ArgumentNullException(
				"Override information mismatch, output measure muist not be null or empty.");
			}

			IsOverridingMeasure = true;
		}
	}

	public DirectoryInfo Info { get; }
	public string SourceMeasure { get; }
	public string OutputMeasure { get; }
	public bool IsOverridingMeasure { get; }
	public bool IsAnySourceMeasure { get; }
	public bool SingleMeasureRequired => IsOverridingMeasure && IsAnySourceMeasure;
}

// -------------------------------------------------------
// ISO Contries and IHME locations mapping file
// -------------------------------------------------------
struct Location
{
	public int Id { get; set; }
	public string Name { get; set; }
	public int IsoCode { get; set; }
	public override string ToString() => $"{Id},{Name},{IsoCode}";
}

private static IReadOnlyDictionary<int, Location> LoadLocations(string fileName)
{
	var locations = new Dictionary<int, Location>();
	if (!File.Exists(fileName))
	{
		throw new ArgumentException($"Locations file {fileName} not found.");
	}

	var timer = new Stopwatch();
	timer.Start();
	using (var reader = new StreamReader(fileName, Encoding.UTF8))
	{
		string line;
		if ((line = reader.ReadLine()) == null)
		{
			Console.WriteLine("File {0} is empty.", fileName);
			return locations;
		}

		int location_id = 0;
		string[] parts = null;
		while ((line = reader.ReadLine()) != null)
		{
			try
			{
				parts = SplitCsv(line);
				if (parts.Length < 2)
				{
					throw new InvalidDataException($"Locations file line: {line}, not a valid format.");
				}

				location_id = int.Parse(parts[0].Trim());
				locations.Add(location_id, new Location
				{
					Id = location_id,
					Name = parts[1].Trim(),
					IsoCode = int.Parse(parts[2].Trim()),
				});
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error parsing line: {0}, cause: {1}", line, ex.Message);
				parts.Dump();
				throw;
			}
		}
	}

	return locations;
}

// -------------------------------------------------------
// Parse IHME datasets generated with GBDx and Epi Tools 
// -------------------------------------------------------

public enum KnownFileFormat { None, GBDx, Epi }

public interface IHMEDataRow
{
	int LocationId { get; }
	string Location { get; }

	int GenderId { get; }
	string Gender { get; }

	int DiseaseId { get; }
	string Disease { get; }

	int AgeGroupId { get; }
	string AgeGroup { get; }

	int MeasureId { get; }
	string Measure { get; set; }

	double Mean { get; }
	double Lower { get; }
	double Upper { get; }

	RowKey ToRowKey();
}

public struct GBDxDataRow : IHMEDataRow
{
	public int MeasureId { get; set; }
	public string Measure { get; set; }

	public int LocationId { get; set; }
	public string Location { get; set; }

	public int GenderId { get; set; }
	public string Gender { get; set; }

	public int AgeGroupId { get; set; }
	public string AgeGroup { get; set; }

	public int DiseaseId { get; set; }
	public string Disease { get; set; }

	public int MetricId { get; set; }
	public string Metric { get; set; }

	public int Year { get; set; }

	public double Mean { get; set; }
	public double Upper { get; set; }
	public double Lower { get; set; }

	public RowKey ToRowKey()
	{
		return new RowKey(this.LocationId, this.Year, this.AgeGroupId, this.GenderId);
	}

	public static IHMEDataRow Parse(string line)
	{
		return Parse(line, 1.0);
	}

	public static IHMEDataRow Parse(string line, double unit_multiplier)
	{
		string[] parts = null;
		if (line.Contains("\""))
		{
			parts = SplitCsv(line);
		}
		else
		{
			parts = line.Split(",");
		}

		if (parts.Length != 16)
		{
			throw new InvalidDataException($"Invalid input format: {line}, must have 16 fields.");
		}

		var metric_name = parts[11].Trim();
		if (!string.Equals(metric_name, "rate", StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException($"Invalid metric type: {metric_name} in data, must be rate.");
		}

		try
		{
			return new GBDxDataRow
			{
				MeasureId = int.Parse(parts[0].Trim()),
				Measure = parts[1].Trim(),

				LocationId = int.Parse(parts[2].Trim()),
				Location = parts[3].Trim(),

				GenderId = int.Parse(parts[4].Trim()),
				Gender = parts[5].Trim(),

				AgeGroupId = int.Parse(parts[6].Trim()),
				AgeGroup = parts[7].Trim(),

				DiseaseId = int.Parse(parts[8].Trim()),
				Disease = parts[9].Trim(),

				MetricId = int.Parse(parts[10].Trim()),
				Metric = metric_name,

				Year = int.Parse(parts[12].Trim()),

				Mean = double.Parse(parts[13].Trim()) * unit_multiplier,
				Lower = double.Parse(parts[14].Trim()) * unit_multiplier,
				Upper = double.Parse(parts[15].Trim()) * unit_multiplier,
			};
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Invalid line format: {line}, {ex.Message}");
			throw;
		}
	}
}

public struct EpiDataRow : IHMEDataRow
{
	public int ModelVersion { get; set; }

	public int DiseaseId { get; set; }
	public string Disease { get; set; }

	public int MeasureId { get; set; }
	public string Measure { get; set; }

	public int LocationId { get; set; }
	public string Location { get; set; }

	public int AgeGroupId { get; set; }
	public string AgeGroup { get; set; }

	public int Year { get; set; }

	public int GenderId { get; set; }
	public string Gender { get; set; }

	public double Mean { get; set; }
	public double Upper { get; set; }
	public double Lower { get; set; }

	public RowKey ToRowKey()
	{
		return new RowKey(this.LocationId, this.Year, this.AgeGroupId, this.GenderId);
	}

	public static IHMEDataRow Parse(string line)
	{
		string[] parts = null;
		if (line.Contains("\""))
		{
			parts = SplitCsv(line);
		}
		else
		{
			parts = line.Split(",");
		}

		if (parts.Length != 15)
		{
			throw new InvalidDataException($"Invalid input format: {line}, must have 15 fields.");
		}

		try
		{
			return new EpiDataRow
			{
				ModelVersion = int.Parse(parts[0].Trim()),

				DiseaseId = int.Parse(parts[1].Trim()),
				Disease = parts[2].Trim(),

				MeasureId = int.Parse(parts[3].Trim()),
				Measure = parts[4].Trim(),

				LocationId = int.Parse(parts[5].Trim()),
				Location = parts[6].Trim(),

				AgeGroupId = int.Parse(parts[7].Trim()),
				AgeGroup = parts[8].Trim(),

				Year = int.Parse(parts[9].Trim()),

				GenderId = int.Parse(parts[10].Trim()),
				Gender = parts[11].Trim(),

				Mean = double.Parse(parts[12].Trim()),
				Lower = double.Parse(parts[13].Trim()),
				Upper = double.Parse(parts[14].Trim()),
			};
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Invalid line format: {line}, {ex.Message}");
			throw;
		}
	}
}

public class IHMEOutputRow
{
	public int LocationId { get; set; }
	public string Location { get; set; }
	public string Disease { get; set; }
	public int Time { get; set; }
	public int AgeGroupId { get; set; }
	public AgeGroup AgeGroup { get; set; }
	public int Age { get; set; }
	public bool IsFilled { get; set; }
	public int GenderId { get; set; }
	public string Gender { get; set; }
	public int MeasureId { get; set; }
	public string Measure { get; set; }
	public double Mean { get; set; }
	public double Upper { get; set; }
	public double Lower { get; set; }
	
	public RowKey ToRowKey()
	{
		return new RowKey(this.LocationId, this.Time, this.AgeGroupId, this.GenderId);
	}

	public override string ToString()
	{
		return ToRowKey().ToString();
	}
	
	public static string CsvHeader()
	{
		return "location_id,location,disease,time,age_group_id,age_group,age,is_filled,gender_id,gender,measure_id,measure,mean,lower,upper";
	}

	public string ToCsv()
	{
		return $"{LocationId},{Location},{Disease},{Time},{AgeGroupId},{AgeGroup},{Age},{IsFilled},{GenderId},{Gender},{MeasureId},{Measure},{Mean},{Lower},{Upper}";
	}
}

public struct Metric
{
public double Value { get; set; }

public double Lower { get; set; }

public double Upper { get; set; }

public override string ToString()
{
	return $"{this.Value}, {this.Lower}, {this.Upper}";
}
}

public class RowKey : IEquatable<RowKey>, IComparable<RowKey>
{
	public RowKey(int locId, int year, int age, int gender)
	{
		this.LocationId = locId;
		this.Year = year;
		this.AgeId = age;
		this.GenderId = gender;
	}

	public int LocationId { get; }
	public int Year { get; }
	public int AgeId { get; }
	public int GenderId { get; }

	public override string ToString()
	{
		return $"{LocationId}, {Year}, {AgeId}, {GenderId}";
	}

	public int CompareTo(RowKey other)
	{
		if (other == null)
			return 1;

		var result = LocationId.CompareTo(other.LocationId);
		if (result == 0)
		{
			result = Year.CompareTo(other.Year);
			if (result == 0)
			{
				result = AgeId.CompareTo(other.AgeId);
				if (result == 0)
				{
					return GenderId.CompareTo(other.GenderId);
				}
			}
		}

		return result;
	}

	public bool Equals(RowKey other)
	{
		if (other == null)
			return false;

		return this.LocationId.Equals(other.LocationId) &&
			   this.Year.Equals(other.Year) &&
			   this.AgeId.Equals(other.AgeId) &&
			   this.GenderId.Equals(other.GenderId);
	}

	public override bool Equals(object obj)
	{
		if (obj == null)
			return false;

		RowKey rowObj = obj as RowKey;
		if (rowObj == null)
			return false;
		else
			return Equals(rowObj);
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(
		this.LocationId.GetHashCode(),
		this.Year.GetHashCode(),
		this.AgeId.GetHashCode(),
		this.GenderId.GetHashCode());
	}

	public static bool operator ==(RowKey left, RowKey right)
	{
		if (((object)left) == null || ((object)right) == null)
			return object.Equals(left, right);

		return left.Equals(right);
	}

	public static bool operator !=(RowKey left, RowKey right)
	{
		return !(left == right);
	}
}

public struct AgeGroup
{
	public int Start { get; set; }
	public int End { get; set; }
	public int Span => End - Start;
	public bool IsEdge { get; set; }

	public override string ToString()
	{
		return $"{Start} to {End}";
	}

	public static AgeGroup Parse(string group, int max_age)
	{
		var index = group.IndexOf("to", StringComparison.OrdinalIgnoreCase);
		if (index > 0)
		{
			var end_index = index + 2;
			return new AgeGroup
			{
				Start = int.Parse(group.Substring(0, index)),
				End = int.Parse(group.Substring(end_index, group.Length - end_index)),
				IsEdge = false,
			};
		}

		if (group.Trim().StartsWith("<"))
		{
			index = group.IndexOf("year", StringComparison.OrdinalIgnoreCase);

			return new AgeGroup
			{
				Start = 0,
				End = int.Parse(group.Substring(1, index - 1)),
				IsEdge = true,
			};
		}

		index = group.IndexOf("plus", StringComparison.OrdinalIgnoreCase);
		if (index > 0)
		{
			return new AgeGroup
			{
				Start = int.Parse(group.Substring(0, index)),
				End = max_age,
				IsEdge = true,
			};
		}

		index = group.IndexOf("Neonatal", StringComparison.OrdinalIgnoreCase);
		if (index > 0)
		{
			return new AgeGroup
			{
				Start = 0,
				End = 0,
				IsEdge = true,
			};
		}

		throw new InvalidCastException($"Invalid age groups format: {group}.");
	}
}

struct ResultInfo
{
	public string DiseaseName { get; set; }
	public IReadOnlyDictionary<int, string> Genders { get; set; }
	public IReadOnlyDictionary<int, AgeGroup> AgeGroups { get; set; }
	public IReadOnlyDictionary<int, string> Measures { get; set; }
	public IReadOnlyDictionary<RowKey, Dictionary<int, Metric>> Data { get; set; }
	public IReadOnlyDictionary<int, string> OverriddenMeasures { get; set; }
	public int NumberOfFiles { get; set; }
	public override string ToString()
	{
		var sw = new StringBuilder();
		sw.AppendLine($"Disease...: {DiseaseName}");
		sw.AppendLine($"Genders...: {string.Join(", ", Genders.Values)}");
		sw.AppendLine($"Measures..: {string.Join(", ", Measures.Values)}");
		sw.AppendLine($"Overrides.: {string.Join(", ", OverriddenMeasures.Values)}");
		sw.AppendLine($"Age Groups: {string.Join(", ", AgeGroups.Values)}");
		sw.AppendLine($"# of File.: {NumberOfFiles}");
		sw.AppendLine($"# of rows.: {Data.Count}");
		return sw.ToString();
	}
}

private static ResultInfo ProcessIHMEDiseaseFolder(IReadOnlyList<DiseaseFolder> sourceFolders,
IReadOnlyDictionary<int, Location> locations, int maximum_age, double unit_multiplier)
{
	Console.WriteLine("The dataset has a total of {0} folder to process.", sourceFolders.Count);
	var timer = new Stopwatch();
	timer.Start();

	var disease = string.Empty;
	var genders = new Dictionary<int, string>();
	var ageGroups = new Dictionary<int, AgeGroup>();
	var measures = new Dictionary<int, string>();

	// Disease: Key[ Location Id, Year, Age Group Id, Gender ID], Values [Metric Id, Metric]
	var summary = new SortedDictionary<RowKey, Dictionary<int, Metric>>();
	var folderMeasure = new Dictionary<DirectoryInfo, string>();
	KnownFileFormat fileFormat;
	var numberOfFiles = 0;
	foreach (var folder in sourceFolders)
	{
		var folderFiles = folder.Info.GetFiles("*.csv");
		Console.WriteLine("\nFolder {0} contains {1} files.", folder.Info.FullName, folderFiles.Length);
		if (folderFiles.Length > 1 && folderFiles.Length != 40)
		{
			throw new ArgumentOutOfRangeException("The number of CSV files in disease folders must be 0, 1 or 40.");
		}
		
		numberOfFiles += folderFiles.Length;
		var currentMeasuresCount = measures.Count;
		foreach (var fileInfo in folderFiles)
		{
			Console.WriteLine("Processing file {0} ... ", fileInfo.FullName);
			using (var reader = new StreamReader(fileInfo.FullName, Encoding.UTF8))
			{
				string line;
				if ((line = reader.ReadLine()) == null)
				{
					Console.WriteLine("File {0} is empty.", fileInfo.FullName);
					continue;
				}

				fileFormat = KnownFileFormat.None;
				if (line.Trim().StartsWith("measure_id", StringComparison.OrdinalIgnoreCase))
				{
					fileFormat = KnownFileFormat.GBDx;
				}
				else if (line.Trim().StartsWith("model_version_id", StringComparison.OrdinalIgnoreCase))
				{
					fileFormat = KnownFileFormat.Epi;
				}
				else
				{
					Console.WriteLine("Unknown file format: {0}.", fileInfo.FullName);
					continue;
				}

				IHMEDataRow row;

				while ((line = reader.ReadLine()) != null)
				{
					if (fileFormat == KnownFileFormat.GBDx)
						row = GBDxDataRow.Parse(line, unit_multiplier);
					else
						row = EpiDataRow.Parse(line);

					if (!locations.ContainsKey(row.LocationId))
					{
						// Console.WriteLine("Unknow location identifier: {0} in line: {1}", row.LocationId, line);
						continue;
					}

					if (disease.Equals(string.Empty))
					{
						disease = row.Disease;
					}
					else if (row.Disease.IndexOf(disease, StringComparison.OrdinalIgnoreCase) < 0 &&
							 disease.IndexOf(row.Disease, StringComparison.OrdinalIgnoreCase) < 0)
					{
						throw new InvalidDataException(
						$"The folder contains multiple diseases: {disease} and {row.Disease} in file: {fileInfo.FullName}.");
					}

					if (!genders.TryGetValue(row.GenderId, out string gender))
					{
						genders.Add(row.GenderId, row.Gender);
					}

					if (!ageGroups.TryGetValue(row.AgeGroupId, out AgeGroup group))
					{
						ageGroups.Add(row.AgeGroupId, AgeGroup.Parse(row.AgeGroup, maximum_age));
					}

					if (!measures.TryGetValue(row.MeasureId, out string measure))
					{
						measures.Add(row.MeasureId, row.Measure);
					}

					// Some file can have multiple measure, this algorithm only support overriding files with a single measure
					if (folder.IsOverridingMeasure &&
					   (folder.IsAnySourceMeasure || string.Equals(row.Measure, folder.SourceMeasure, StringComparison.OrdinalIgnoreCase)))
					{
						row.Measure = folder.OutputMeasure;
					}

					var rowKey = row.ToRowKey();
					if (summary.TryGetValue(rowKey, out Dictionary<int, Metric> metrics))
					{
						if (metrics.ContainsKey(row.MeasureId))
						{
							throw new InvalidDataException(
							$"The folder contains duplicated measure entries for: {measures[row.MeasureId]} in file: {fileInfo.FullName}.");
						}

						metrics.Add(row.MeasureId, new Metric { Value = row.Mean, Lower = row.Lower, Upper = row.Upper });
					}
					else
					{
						summary.Add(rowKey, new Dictionary<int, Metric>{
							{row.MeasureId, new Metric { Value = row.Mean, Lower = row.Lower, Upper = row.Upper}}
						});
					}
				}
			}
		}

		var fileNewMeasures = measures.Count - currentMeasuresCount;
		if (folder.SingleMeasureRequired)
		{
			if (fileNewMeasures > 1)
			{
				throw new InvalidDataException(
				$"Folder contains multiple overriding measures: {fileNewMeasures} in file: {folder.Info.FullName}.");
			}

			folderMeasure.Add(folder.Info, measures.Last().Value);
		}
	}

	var replacedMeasures = new Dictionary<int, string>();
	foreach (var folder in sourceFolders)
	{
		if (!folder.IsOverridingMeasure)
		{
			continue;
		}

		var measure = folder.SourceMeasure;
		if (folder.IsAnySourceMeasure)
		{
			measure = folderMeasure[folder.Info];
		}

		var entry = measures.FirstOrDefault(s => s.Value.Equals(measure, StringComparison.OrdinalIgnoreCase));
		if (measures.ContainsKey(entry.Key))
		{
			replacedMeasures[entry.Key] = measures[entry.Key];
			measures[entry.Key] = folder.OutputMeasure;
		}
	}

	timer.Stop();
	Console.WriteLine("Completed, {0} files processed, elapsed time: {1} seconds", numberOfFiles, timer.Elapsed.TotalSeconds);

	return new ResultInfo
	{
		DiseaseName = disease,
		Genders = genders,
		AgeGroups = ageGroups,
		Measures = measures,
		OverriddenMeasures = replacedMeasures,
		NumberOfFiles = numberOfFiles,
		Data = summary
	};
}

private static HashSet<int> ApplySmoothingToMeasuresInplace(
IReadOnlyDictionary<int, List<IHMEOutputRow>> countries,
IReadOnlyDictionary<int, string> measures, int smoothing_times)
{
	Console.WriteLine("\nFilling gaps, smoothing measures and calculating insidence ...");
	var timer = new Stopwatch();
	timer.Start();

	var prevalence_id = measures.First(s => s.Value.Equals("Prevalence", StringComparison.OrdinalIgnoreCase)).Key;
	var incidence_id = measures.First(s => s.Value.Equals("Incidence", StringComparison.OrdinalIgnoreCase)).Key;
	var remission_id = measures.First(s => s.Value.Equals("Remission", StringComparison.OrdinalIgnoreCase)).Key;
	var mortality_id = measures.First(s => s.Value.Equals("Mortality", StringComparison.OrdinalIgnoreCase)).Key;

	var inclomplete = new HashSet<int>();
	var result = new SortedDictionary<int, List<IHMEOutputRow>>();
	foreach (var country_key in countries.Keys)
	{
		var country = countries[country_key];
		var start_age = country.First().Age;
		var maleMeasures = new Dictionary<int, Dictionary<int, double>>(country.Count);
		var femaleMeasures = new Dictionary<int, Dictionary<int, double>>(country.Count);
		var countryMissingData = false;
		foreach (var measure in measures)
		{
			if (measure.Key == incidence_id)
			{
				continue;
			}

			var smoothMale = new Dictionary<int, double>(country.Count);
			var smoothFemale = new Dictionary<int, double>(country.Count);
			for (var index = 0; index < country.Count; index++)
			{
				var row = country[index];
				if (row.MeasureId != measure.Key)
				{
					continue;
				}

				if (row.Gender.Equals("male", StringComparison.OrdinalIgnoreCase))
				{
					smoothMale.Add(row.Age, row.Mean);
				}
				else
				{
					smoothFemale.Add(row.Age, row.Mean);
				}
			}
			try
			{
				if (smoothMale.Count > 1 && smoothFemale.Count > 1)
				{
					SmoothDataInplace(smoothing_times, smoothMale);
					SmoothDataInplace(smoothing_times, smoothFemale);
				}
				else
				{
					countryMissingData = true;
					Console.WriteLine("No data for measure: {0} for male: {1}, female: {2} in country: {3}", measure.Value, smoothMale.Count < 1, smoothFemale.Count < 1, country_key);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error processing: {0} for country: {1}.", measure.Value, country_key);
				throw;
			}

			for (var index = 0; index < country.Count; index++)
			{
				var row = country[index];
				if (row.MeasureId != measure.Key)
				{
					continue;
				}

				if (row.Gender.Equals("male", StringComparison.OrdinalIgnoreCase))
				{
					row.Mean = smoothMale[row.Age];
					if (!maleMeasures.ContainsKey(row.Age))
					{
						maleMeasures.Add(row.Age, new Dictionary<int, double>());
					}

					maleMeasures[row.Age][measure.Key] = row.Mean;
				}
				else
				{
					row.Mean = smoothFemale[row.Age];
					if (!femaleMeasures.ContainsKey(row.Age))
					{
						femaleMeasures.Add(row.Age, new Dictionary<int, double>());
					}

					femaleMeasures[row.Age][measure.Key] = row.Mean;
				}
			}
		}

		// Recalculate Insidence
		if (countryMissingData)
		{
			inclomplete.Add(country_key);
			continue;
		}

		for (var index = 0; index < country.Count; index++)
		{
			var row = country[index];
			if (row.MeasureId != incidence_id)
			{
				continue;
			}

			if (row.Gender.Equals("male", StringComparison.OrdinalIgnoreCase))
			{
				var p_male = row.Age == start_age ? 0.0 : maleMeasures[row.Age - 1][prevalence_id];
				var male_measure = maleMeasures[row.Age];
				var male_value = 1.0 - male_measure[remission_id] - male_measure[mortality_id];
				male_value = (male_measure[prevalence_id] - male_value * p_male) / (1.0 - p_male);
				row.Mean = Math.Max(Math.Min(male_value, 1.0), 0.0);
			}
			else
			{
				var p_female = row.Age == start_age ? 0.0 : femaleMeasures[row.Age - 1][prevalence_id];
				var female_measure = femaleMeasures[row.Age];
				var female_value = 1.0 - female_measure[remission_id] - female_measure[mortality_id];
				female_value = (female_measure[prevalence_id] - female_value * p_female) / (1.0 - p_female);
				row.Mean = Math.Max(Math.Min(female_value, 1.0), 0.0);
			}
		}
	}

	timer.Stop();
	Console.WriteLine("Completed, elapsed time: {0} seconds", timer.Elapsed.TotalSeconds);
	return inclomplete;
}
