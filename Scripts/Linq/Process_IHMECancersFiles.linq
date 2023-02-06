<Query Kind="Program">
  <Output>DataGrids</Output>
  <Namespace>System.Text.Json</Namespace>
  <Namespace>System.Text.Json.Serialization</Namespace>
</Query>

#load "Process_common.linq"
#load "Process_IHMEShared.linq"

void Main()
{
	//var patchFile = new FileInfo(@"C:\Work\Data\IHME\Cancers\Source\Ovary Cancer\2019\IHME-GBD_2019_DATA-a5f3072b-1.csv");
 	//PatchCancerFileWithGender(patchFile);
	//return;
	
	var doc = JsonDocument.Parse(File.ReadAllText(@"C:\Work\Data\IHME\diseases.json"));
	ValidateSchemaVersion(doc);
	var settings = GetDiseaseSettings(doc);

	var cancers_root_folder = Path.Combine(settings.DataRootFolder, settings.CancersFolder);
	var locations_file_fullname = Path.Combine(settings.DataRootFolder, settings.LocationFilename);

	Console.WriteLine("*** Loading IHME Locations and ISO mapping. ***");
	var locations = LoadLocations(locations_file_fullname);

	if (!doc.RootElement.TryGetProperty("cancers", out JsonElement cancers))
	{
		throw new InvalidDataException("Invalid configuration file format, missing cancers.");
	}
	
	var numberIgnored = 0;
	var numberProcessed = 0;
	foreach (var entry in cancers.EnumerateObject())
	{
		var cancer = entry.Value.Deserialize<Cancer>();
		cancer.Id = entry.Name;

		// Configure the cancer source dataset folders
		var data_subfolder = @$"{settings.SourceFolder}\{cancer.Name}\{settings.SourceYear}";
		var cancer_root_folder = Path.Combine(cancers_root_folder, data_subfolder);
		var rawparameter_folder = new DirectoryInfo(Path.Combine(cancers_root_folder, @$"{settings.SourceFolder}\{cancer.Name}\{cancer.ParametersFolder}"));

		var cancer_data_folders = new List<DiseaseFolder>(){
			new DiseaseFolder(new DirectoryInfo(cancer_root_folder)),
		};

		foreach (var item in cancer.InputFolders)
		{
			var folder = item.Value;
			if (!string.IsNullOrWhiteSpace(folder))
			{
				var folderInfo = new DirectoryInfo(Path.Combine(cancer_root_folder, folder));
				var subfolder = new DiseaseFolder(folderInfo);
				if (item.Key.Equals("mortality", StringComparison.OrdinalIgnoreCase))
				{
					subfolder = new DiseaseFolder(folderInfo, "*", "mortality");
				}

				cancer_data_folders.Add(subfolder);
			}
		}

		// Configure the cancer processed output folders - disease.Id
		var output_subfolder_name = @$"{settings.OutputSubFolder}\{cancer.Name}";
		var cancer_output_folder = new DirectoryInfo(Path.Combine(cancers_root_folder, output_subfolder_name));
		try
		{
			if (cancer_output_folder.Exists)
			{
				if (!cancer.Overwrite)
				{
					numberIgnored++;
					Console.WriteLine("# Ignoring: {0}, overwriting is disabled.", cancer.Id);
					continue;
				}

				cancer_output_folder.Delete(true);
			}

			numberProcessed++;
			Console.WriteLine("\n# Processing: {0} in folder: {1} ...", cancer.Id, cancer_root_folder);
			cancer_output_folder.Create();

			// Convert raw parameter files to the required format
			CreateCancerParametersFolder(new DiseaseFolder(rawparameter_folder), settings.CancerYears, cancer_data_folders.First());

			// 3. Process IHME disease data file
			var definitions = ProcessIHMEDiseaseFolder(cancer_data_folders, locations, settings.MaxAge, settings.UnitsMultiplier);
			Console.WriteLine("\nResults summary:\n{0}", definitions.ToString());

			// 4. Process Cancer Parameters
			var parameters = ProcessCancerParametersFolder(cancer_data_folders, settings.SourceYear, locations);

			// 5. Create counties with filled age gaps and apply smoothing function!!
			var add_measures = new Dictionary<int, string> { { Remission_ID, "remission" }, { Mortality_ID, "mortality" } };
			var countries = CreateCountriesDatasetWithFilledAgeGaps(definitions, parameters, add_measures, locations);

			var all_measures = new Dictionary<int, string>(definitions.Measures);
			foreach (var item in add_measures)
			{
				all_measures.Add(item.Key, item.Value);
			}

			var incomplete = ApplySmoothingToMeasuresInplace(countries, all_measures, settings.SmoothingTimes);

			// 6. Write data to CSV file
			WriteCancerDefinitionToCsv(countries, parameters, incomplete, cancer_output_folder);
		}
		catch (Exception ex)
		{
			Console.WriteLine("# Failed to process: {0}, cause: {1}", cancer.Id, ex.Message);
			throw; // Stop processing, remove to continue to next cancer.
		}
	}

	Console.WriteLine("\n# Processed {0} and ignored {1} cancers respectively.", numberProcessed, numberIgnored);
}

public class Cancer
{
	[JsonPropertyName("id")]
	public string Id { get; set; }

	[JsonPropertyName("overwrite")]
	public bool Overwrite { get; set; }

	[JsonPropertyName("folder_name")]
	public string Name { get; set; }

	[JsonPropertyName("input_folders")]
	public Dictionary<string, string> InputFolders { get; set; }

	[JsonPropertyName("raw_parameters_folder")]
	public string ParametersFolder { get; set; }
}

// --------------------------------------------
// Cancers Calculated Measures Identifiers
// --------------------------------------------
private static readonly int Remission_ID = 7;
private static readonly int Mortality_ID = 15;

// --------------------------------------------
// Cancers Paramaters Data Structures
// --------------------------------------------
public class GenderValue
{
	public GenderValue(double maleValue, double femaleValue)
	{
		this.Male = maleValue;
		this.Female = femaleValue;
	}

	public double Male { get; }

	public double Female { get; }

	public override string ToString()
	{
		return $"Male: {Male}, Female: {Female}";
	}
}

public class LocationParameter
{
	public LocationParameter(int dataReferenceYear)
	{
		if (dataReferenceYear < 1950)
		{
			throw new ArgumentOutOfRangeException("The model data reference year must not be less than 1950.");
		}

		TimeYear = dataReferenceYear;
	}

	public int TimeYear { get; }
	public IReadOnlyDictionary<int, GenderValue> PrevalenceDistribution { get; set; }
	public IReadOnlyDictionary<int, GenderValue> SurvivalRate { get; set; }
	public IReadOnlyDictionary<int, GenderValue> DeathWeight { get; set; }

	public static string CsvHeader()
	{
		return "Time,Male,Female";
	}

	public static string ToCsv(int rowKey, GenderValue rowValue)
	{
		return $"{rowKey},{rowValue.Male},{rowValue.Female}";
	}
}

// --------------------------------------------
// Cancers Survival Rates calculation
// --------------------------------------------

public static double CalculateSurvivalRate(IReadOnlyDictionary<int, GenderValue> survival, string gender, int age, int time_year)
{
	var result = 0.0;
	if (gender.Equals("male", StringComparison.OrdinalIgnoreCase))
	{
		result = survival[1].Male;
		result += survival[2].Male * time_year;
		result += survival[3].Male * time_year * time_year;
		result += survival[4].Male * time_year * time_year * time_year;
		result += survival[5].Male * age;
		result += survival[6].Male * age * age;
		result += survival[7].Male * age * age * age;
		result = Math.Max(Math.Min(result, 1.0), 0.0);
		return result;
	}

	result = survival[1].Female;
	result += survival[2].Female * time_year;
	result += survival[3].Female * time_year * time_year;
	result += survival[4].Female * time_year * time_year * time_year;
	result += survival[5].Female * age;
	result += survival[6].Female * age * age;
	result += survival[7].Female * age * age * age;
	result = Math.Max(Math.Min(result, 1.0), 0.0);
	return result;
}

private static double CalculateMeasure(int measureId, LocationParameter locParameter, string genderName, int age)
{
	if (measureId == Remission_ID)
	{
		return 0.0;
	}

	// Calculate Mortality
	if (measureId != Mortality_ID)
	{
		throw new InvalidOperationException("Unknown measure identification for calculation.");
	}

	if (locParameter == null)
	{
		throw new InvalidOperationException("The location parameters are needed for mortality calculation.");
	}
	else if (string.IsNullOrWhiteSpace(genderName))
	{
		throw new ArgumentNullException("The gender name must not be null or empty.");
	}
	else if (age < 0)
	{
		throw new ArgumentOutOfRangeException("The 'age' parameter must not be negative.");
	}

	var mortality = CalculateSurvivalRate(locParameter.SurvivalRate, genderName, age, locParameter.TimeYear);
	return 1.0 - mortality;
}

// --------------------------------------------
// Cancer data processing algorithms
// --------------------------------------------
void CreateCancerParametersFolder(DiseaseFolder rawDataFolder, int cancerYears, DiseaseFolder outputFolder)
{
	var timer = new Stopwatch();
	timer.Start();

	var maleFiles = new Dictionary<string, FileInfo>();
	var femaleFiles = new Dictionary<string, FileInfo>();
	var countries = rawDataFolder.Info.GetDirectories();
	var deathWeightConstant = 1.0 / (double)cancerYears;
	foreach (var countryFolder in countries)
	{
		Console.WriteLine("Creating {0} cancers parameters...", countryFolder.Name);
		var folderFiles = countryFolder.GetFiles("*.csv");
		foreach (var fileInfo in folderFiles)
		{
			var fileName = fileInfo.Name;
			if (fileName.Contains("5YearSurvivalRates", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			if (fileName.Contains(".Male.", StringComparison.OrdinalIgnoreCase))
			{
				maleFiles.Add(fileInfo.Name, fileInfo);
			}
			else if (fileName.Contains(".Female.", StringComparison.OrdinalIgnoreCase))
			{
				femaleFiles.Add(fileInfo.Name, fileInfo);
			}
			else
			{
				Console.WriteLine($"Unknown file in parameters folder: {fileName}, ignored.");
			}
		}

		if (maleFiles.Count != femaleFiles.Count)
		{
			throw new InvalidDataException($"Invalid cancer parameter folder content: {countryFolder.Name}");
		}

		var countryOutputFolder = Path.Combine(outputFolder.Info.FullName, countryFolder.Name);
		if (!Directory.Exists(countryOutputFolder))
		{
			Directory.CreateDirectory(countryOutputFolder);
		}

		foreach (var fileEntry in maleFiles)
		{
			var femaleFileKey = fileEntry.Key.Replace("Male", "Female");
			var outputFileName = string.Empty;
			if (fileEntry.Key.Contains("DeathWeights", StringComparison.OrdinalIgnoreCase))
			{
				outputFileName = Path.Combine(countryOutputFolder, "death_weights.csv");
			}
			else if (fileEntry.Key.Contains("DistributionOver5Years", StringComparison.OrdinalIgnoreCase))
			{
				outputFileName = Path.Combine(countryOutputFolder, "prevalence_distribution.csv");
			}
			else if (fileEntry.Key.Contains("SurvivalRatesParameters", StringComparison.OrdinalIgnoreCase))
			{
				outputFileName = Path.Combine(countryOutputFolder, "survival_rate_parameters.csv");
			}
			else
			{
				Console.WriteLine($"Unknown file in parameters folder: {fileEntry.Key}, ignored.");
				continue;
			}

			var femaleFile = femaleFiles[femaleFileKey];
			var maleConstant = false;
			var femaleConstant = false;
			using (var sw = new StreamWriter(outputFileName))
			{
				sw.WriteLine("Time,Male,Female");
				using (var maleReader = fileEntry.Value.OpenText())
				using (var femaleReader = femaleFile.OpenText())
				{
					var maleLine = maleReader.ReadLine();
					var femaleLine = femaleReader.ReadLine();
					if (!maleLine.StartsWith("0"))
					{
						// Throw away header
						maleLine = maleReader.ReadLine();
						femaleLine = femaleReader.ReadLine();
					}

					while (maleLine != null && femaleLine != null)
					{
						var maleFields = maleLine.Split(",").Select(l => l.Replace("\"", "").Trim()).ToList();
						var femaleFields = femaleLine.Split(",").Select(l => l.Replace("\"", "").Trim()).ToList();
						if (maleFields[0] != femaleFields[0])
						{
							throw new InvalidDataException($"Male and female step mismatch: {maleFields[0]} vs. {femaleFields[0]} in {outputFileName}");
						}
						
						if (!double.TryParse(maleFields[1], out double maleValue))
						{
							maleConstant = true;
							maleValue = deathWeightConstant;
						}
						
						if (!double.TryParse(femaleFields[1], out double femaleValue))
						{
							femaleConstant = true;
							femaleValue = deathWeightConstant;
						}
						
						sw.WriteLine("{0},{1},{2}", maleFields[0], maleValue, femaleValue);
						maleLine = maleReader.ReadLine();
						femaleLine = femaleReader.ReadLine();
					}
				}
			}
			
			if (maleConstant == true || femaleConstant == true)
			{
				Console.WriteLine($" - File: {outputFileName} using constant values for male: {maleConstant}, female: {femaleConstant}.");
			}
		}

		maleFiles.Clear();
		femaleFiles.Clear();
	}

	timer.Stop();
	Console.WriteLine("Completed, {0} countries, elapsed time: {1} seconds", countries.Length, timer.Elapsed.TotalSeconds);
}

IReadOnlyDictionary<int, LocationParameter> ProcessCancerParametersFolder(
List<DiseaseFolder> sourceFolders, int modelDataYear, IReadOnlyDictionary<int, Location> locations)
{
	var timer = new Stopwatch();
	timer.Start();
	Console.WriteLine("Processing file cancer parameter datasets ...");
	Console.WriteLine("The dataset has a total of {0} folder to process.\n", sourceFolders.Count);

	var countries = new Dictionary<int, LocationParameter>();
	foreach (var folder in sourceFolders)
	{
		var paramFolders = folder.Info.GetDirectories();
		Console.WriteLine("Folder {0} contains {1} directories.", folder.Info.FullName, paramFolders.Length);
		foreach (var subFolder in paramFolders)
		{
			var country = locations.FirstOrDefault(
				s => s.Value.Name.Equals(subFolder.Name, StringComparison.OrdinalIgnoreCase));
			if (country.Key <= 0)
			{
				continue;
			}

			var folderFiles = subFolder.GetFiles("*.csv");
			var parameter = new LocationParameter(modelDataYear);
			var lastFileName = "unknown";
			try
			{
				foreach (var fileInfo in folderFiles)
				{
					lastFileName = fileInfo.FullName;
					var fileData = LoadCancerParameterFile(fileInfo);
					if (fileInfo.Name.StartsWith("death", StringComparison.OrdinalIgnoreCase))
					{
						parameter.DeathWeight = fileData;
					}
					else if (fileInfo.Name.StartsWith("prevalence", StringComparison.OrdinalIgnoreCase))
					{
						parameter.PrevalenceDistribution = fileData;
					}
					else if (fileInfo.Name.StartsWith("survival", StringComparison.OrdinalIgnoreCase))
					{
						parameter.SurvivalRate = fileData;
					}
					else
					{
						Console.WriteLine("Unknow file name in parameter: {0}", fileInfo.FullName);
					}
				}
				
				countries.Add(country.Value.IsoCode, parameter);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Failed to load cancer parameter file: {lastFileName}, cause: {ex.Message}");
				throw;
			}
		}
	}

	timer.Stop();
	Console.WriteLine("Completed, {0} locations processed, elapsed time: {1} seconds", countries.Count, timer.Elapsed.TotalSeconds);

	return countries;
}

private static IReadOnlyDictionary<int, GenderValue> LoadCancerParameterFile(FileInfo fileInfo)
{
	var result = new Dictionary<int, GenderValue>();
	using (var reader = new StreamReader(fileInfo.FullName, Encoding.UTF8))
	{
		var line = string.Empty;
		if ((line = reader.ReadLine()) == null)
		{
			throw new InvalidOperationException($"Attempting to process an empty file: {fileInfo.FullName}");
		}

		var fields = line.Split(",");
		if (fields.Length != 3)
		{
			throw new InvalidDataException($"Invalid cancer parameter file format: {fileInfo.FullName}");
		}

		while ((line = reader.ReadLine()) != null)
		{
			fields = line.Split(",");
			if (fields.Length != 3)
			{
				throw new InvalidDataException($"Invalid cancer parameter format: {line} in file: {fileInfo.FullName}");
			}

			var x = int.Parse(fields[0]);
			var y = new GenderValue(double.Parse(fields[1]), double.Parse(fields[2]));
			result.Add(x, y);
		}
	}

	return result;
}

private static IReadOnlyDictionary<int, List<IHMEOutputRow>> CreateCountriesDatasetWithFilledAgeGaps(
ResultInfo results, IReadOnlyDictionary<int, LocationParameter> parameters,
IReadOnlyDictionary<int, string> newMeasures, IReadOnlyDictionary<int, Location> locations)
{
	var timer = new Stopwatch();
	timer.Start();

	Console.WriteLine("\nCreating location datasets with filled age gaps ...");

	var summary = new SortedDictionary<int, List<IHMEOutputRow>>();
	var is_filled = false;
	var numberOfMetrics = results.Measures.Count;
	var ignoredCounties = new Dictionary<int, string>();
	foreach (var entry in results.Data)
	{
		if (entry.Value.Count != numberOfMetrics)
		{
			Console.WriteLine("# of metrics mismatch in row: {0,-20} ({1} vs. {2})  -  {3} aged {4} in {5}",
							  entry.Key, entry.Value.Count, numberOfMetrics,
							  results.Genders[entry.Key.GenderId], results.AgeGroups[entry.Key.AgeId],
							  locations[entry.Key.LocationId].Name);
		}

		var loc = locations[entry.Key.LocationId];
		if (parameters.ContainsKey(loc.IsoCode))
		{
			if (!summary.ContainsKey(loc.IsoCode))
			{
				summary.Add(loc.IsoCode, new List<IHMEOutputRow>());
			}
		}
		else
		{
			if (!ignoredCounties.ContainsKey(loc.IsoCode))
			{
				ignoredCounties.Add(loc.IsoCode, loc.Name);
			}

			continue;
		}

		var locParameters = parameters[loc.IsoCode];
		var age_group = results.AgeGroups[entry.Key.AgeId];
		var gender_name = results.Genders[entry.Key.GenderId];
		foreach (var metric in entry.Value)
		{
			is_filled = false;
			var measure_name = results.Measures[metric.Key];
			for (var age = age_group.Start; age <= age_group.End; age++)
			{
				summary[loc.IsoCode].Add(new IHMEOutputRow
				{
					LocationId = loc.IsoCode,
					Location = loc.Name,
					Disease = results.DiseaseName,
					Time = entry.Key.Year,
					AgeGroupId = entry.Key.AgeId,
					AgeGroup = age_group,
					Age = age,
					IsFilled = is_filled,
					GenderId = entry.Key.GenderId,
					Gender = gender_name,
					MeasureId = metric.Key,
					Measure = measure_name,
					Mean = metric.Value.Value,
					Lower = metric.Value.Lower,
					Upper = metric.Value.Upper
				});

				is_filled = true;
			}
		}

		// Create new measures
		is_filled = true;
		foreach (var measure in newMeasures)
		{
			for (var age = age_group.Start; age <= age_group.End; age++)
			{
				var measureValue = CalculateMeasure(measure.Key, locParameters, gender_name, age);
				summary[loc.IsoCode].Add(new IHMEOutputRow
				{
					LocationId = loc.IsoCode,
					Location = loc.Name,
					Disease = results.DiseaseName,
					Time = entry.Key.Year,
					AgeGroupId = entry.Key.AgeId,
					AgeGroup = age_group,
					Age = age,
					IsFilled = is_filled,
					GenderId = entry.Key.GenderId,
					Gender = gender_name,
					MeasureId = measure.Key,
					Measure = measure.Value,
					Mean = measureValue,
					Lower = measureValue,
					Upper = measureValue
				});
			}
		}
	}

	timer.Stop();
	Console.WriteLine("Completed, created {0} locations, ignored {1}. Elapsed time: {2} seconds",
					  summary.Count, ignoredCounties.Count, timer.Elapsed.TotalSeconds);
	return summary;
}

private static void WriteCancerDefinitionToCsv(
IReadOnlyDictionary<int, List<IHMEOutputRow>> countries,
IReadOnlyDictionary<int, LocationParameter> parameters,
HashSet<int> toIgnore,
DirectoryInfo ouputFolderInfo)
{
	var timer = new Stopwatch();
	timer.Start();
	Console.WriteLine("\nWriting processed cancer data to output files ...");
	var filesCreatedCount = 0;
	try
	{
		foreach (var entry in countries)
		{
			if (!parameters.ContainsKey(entry.Key) || toIgnore.Contains(entry.Key))
			{
				continue;
			}

			// Create new definition file
			var location_file = Path.Combine(ouputFolderInfo.FullName, $"D{entry.Key}.csv");
			using (var sw = new StreamWriter(location_file, true, Encoding.UTF8))
			{
				sw.WriteLine(IHMEOutputRow.CsvHeader());
				foreach (var row in entry.Value)
				{
					sw.WriteLine(row.ToCsv());
				}
			}

			// Creates location parameters folder and write files
			var locationParameterFolder = Path.Combine(ouputFolderInfo.FullName, $"P{entry.Key}");
			if (!Directory.Exists(locationParameterFolder))
			{
				Directory.CreateDirectory(locationParameterFolder);
			}

			WriteCancerParameterToCsv(Path.Combine(locationParameterFolder, "death_weights.csv"), parameters[entry.Key].DeathWeight);
			WriteCancerParameterToCsv(Path.Combine(locationParameterFolder, "prevalence_distribution.csv"), parameters[entry.Key].PrevalenceDistribution);
			WriteCancerParameterToCsv(Path.Combine(locationParameterFolder, "survival_rate_parameters.csv"), parameters[entry.Key].SurvivalRate);
			filesCreatedCount++;
		}
	}
	catch (Exception ex)
	{
		Console.WriteLine("Error Writing to file: {0}", ex.Message);
		throw;
	}
	finally
	{
		timer.Stop();
		Console.WriteLine("Completed, {0} files created, elapsed time: {1} seconds", filesCreatedCount, timer.Elapsed.TotalSeconds);
	}
}

private static void WriteCancerParameterToCsv(string fileName, IReadOnlyDictionary<int, GenderValue> data)
{
	using (var sw = new StreamWriter(fileName, false, Encoding.UTF8))
	{
		sw.WriteLine(LocationParameter.CsvHeader());
		foreach (var entry in data)
		{
			sw.WriteLine(LocationParameter.ToCsv(entry.Key, entry.Value));
		}
	}
}

private static void WriteTestFileToCsv(string fileName, IReadOnlyList<IHMEOutputRow> country, IReadOnlyDictionary<int, string> measures)
{
	Console.WriteLine("\nWriting test cancer data to file: {0} ...", fileName);
	var timer = new Stopwatch();
	timer.Start();

	var prevalence_id = measures.First(s => s.Value.Equals("Prevalence", StringComparison.OrdinalIgnoreCase)).Key;
	var incidence_id = measures.First(s => s.Value.Equals("Incidence", StringComparison.OrdinalIgnoreCase)).Key;
	var remission_id = measures.First(s => s.Value.Equals("Remission", StringComparison.OrdinalIgnoreCase)).Key;
	var mortality_id = measures.First(s => s.Value.Equals("Mortality", StringComparison.OrdinalIgnoreCase)).Key;

	var start_age = country.First().Age;
	var maleMeasures = new Dictionary<int, Dictionary<int, double>>(country.Count);
	var femaleMeasures = new Dictionary<int, Dictionary<int, double>>(country.Count);
	foreach (var measure in measures)
	{
		for (var index = 0; index < country.Count; index++)
		{
			var row = country[index];
			if (row.MeasureId != measure.Key)
			{
				continue;
			}

			if (row.Gender.Equals("male", StringComparison.OrdinalIgnoreCase))
			{
				if (!maleMeasures.ContainsKey(row.Age))
				{
					maleMeasures.Add(row.Age, new Dictionary<int, double>());
				}

				maleMeasures[row.Age][measure.Key] = row.Mean;
			}
			else
			{
				if (!femaleMeasures.ContainsKey(row.Age))
				{
					femaleMeasures.Add(row.Age, new Dictionary<int, double>());
				}

				femaleMeasures[row.Age][measure.Key] = row.Mean;
			}
		}
	}

	using (var sw = new StreamWriter(fileName, false, Encoding.UTF8))
	{
		sw.WriteLine("gender,age,prevalence,incidence,remission,mortality");
		foreach (var entry in maleMeasures)
		{
			sw.WriteLine("male,{0},{1:g17},{2:g17},{3:g17},{4:g17}", entry.Key, entry.Value[prevalence_id],
			entry.Value[incidence_id], entry.Value[remission_id], entry.Value[mortality_id]);
		}

		foreach (var entry in femaleMeasures)
		{
			sw.WriteLine("female,{0},{1:g17},{2:g17},{3:g17},{4:g17}", entry.Key, entry.Value[prevalence_id],
			entry.Value[incidence_id], entry.Value[remission_id], entry.Value[mortality_id]);
		}
	}

	timer.Stop();
	Console.WriteLine("Completed, elapsed time: {0} seconds", timer.Elapsed.TotalSeconds);
}

record struct CancerRowKey(string MeasureId, string LocId, string AgeId, string GenderId);

private static void PatchCancerFileWithGender(FileInfo source)
{
	if (!source.Exists)
	{
		throw new FileNotFoundException($"Source file: {source.FullName} not found.");
	}
	
	var output = Path.Combine(source.DirectoryName, source.Name.Replace(source.Extension, $"_patch{source.Extension}"));
	if (File.Exists(output))
	{
		File.Delete(output);
	}
	
	using (var sw = new StreamWriter(output, false, Encoding.UTF8))
	using (var sr = source.OpenText())
	{
		var line = string.Empty;
		if ((line = sr.ReadLine()) == null)
		{
			throw new InvalidOperationException($"Attempting to patch an empty file: {source.FullName}");
		}
		
		sw.WriteLine(line);
		var fields = line.Split(",");
		var fieldCount = fields.Length;
		var measureIdx = Array.IndexOf(fields, "measure_id");
		var locationIdx = Array.IndexOf(fields, "location_id");
		var ageIdx = Array.IndexOf(fields, "age_id");
		var genderIdx = Array.IndexOf(fields, "sex_id");
		var valueIdx = Array.IndexOf(fields, "val");

		string[] lastFields = null;
		
		var lastMeasureId = string.Empty;
		var lastLocationId = string.Empty;
		var lastAgeId = string.Empty;
		var lastGenderId = string.Empty;
		var locationSingleGender = false;
		while ((line = sr.ReadLine()) != null)
		{
			fields = line.Split(",");
			if (fields.Length != fieldCount)
			{
				throw new InvalidDataException($"Invalid cancer file format: {line} in file: {source.FullName}");
			}

			var measureId = fields[measureIdx].Trim();
			var locationId = fields[locationIdx].Trim();
			var ageId = fields[ageIdx].Trim();
			var genderId = fields[genderIdx].Trim();

			if (!measureId.Equals(lastMeasureId))
			{
				lastMeasureId = measureId;
				lastLocationId = locationId;
				locationSingleGender = false;
			}
			else if (!locationId.Equals(lastLocationId))
			{
				lastLocationId = locationId;
				locationSingleGender = false;
			}
			else
			{
				if (!ageId.Equals(lastAgeId) && genderId.Equals(lastGenderId))
				{
					// Missing gender
					if (genderId.Equals("1"))
					{
						lastFields[genderIdx] = "2";
						lastFields[genderIdx + 1] = "Female";
					}
					else if (genderId.Equals("2"))
					{
						lastFields[genderIdx] = "1";
						lastFields[genderIdx + 1] = "Male";
					}

					// Update values
					lastFields[valueIdx] = "0.0";
					lastFields[valueIdx + 1] = "0.0";
					lastFields[valueIdx + 2] = "0.0";

					sw.WriteLine(string.Join(",", lastFields));
					locationSingleGender = true;
				}
			}

			lastAgeId = ageId;
			lastGenderId = genderId;
			lastFields = fields;
			sw.WriteLine(line);
			
			// 95 Plus age for Female onle countries
			if (ageId == "235" && locationSingleGender)
			{
				// Missing gender
				if (genderId.Equals("1"))
				{
					lastFields[genderIdx] = "2";
					lastFields[genderIdx + 1] = "Female";
				}
				else if (genderId.Equals("2"))
				{
					lastFields[genderIdx] = "1";
					lastFields[genderIdx + 1] = "Male";
				}

				// Update values
				lastFields[valueIdx] = "0.0";
				lastFields[valueIdx + 1] = "0.0";
				lastFields[valueIdx + 2] = "0.0";

				sw.WriteLine(string.Join(",", lastFields));
			}
		}
	}
}
