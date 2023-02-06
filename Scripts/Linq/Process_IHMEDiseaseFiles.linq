<Query Kind="Program">
  <Output>DataGrids</Output>
  <Namespace>System.Text.Json</Namespace>
  <Namespace>System.Text.Json.Serialization</Namespace>
</Query>

#load "Process_common.linq"
#load "Process_IHMEShared.linq"

void Main()
{
	var doc = JsonDocument.Parse(File.ReadAllText(@"C:\Work\Data\IHME\diseases.json"));
	ValidateSchemaVersion(doc);
	var settings = GetDiseaseSettings(doc);
	
	var diseases_root_folder = Path.Combine(settings.DataRootFolder, settings.DiseasesFolder);
	var locations_file_fullname = Path.Combine(settings.DataRootFolder, settings.LocationFilename);
	
	Console.WriteLine("*** Loading IHME Locations to ISO mapping. ***");
	var locations = LoadLocations(locations_file_fullname);

	if (!doc.RootElement.TryGetProperty("diseases", out JsonElement diseases))
	{
		throw new InvalidDataException("Invalid configuration file format, missing diseases.");
	}

	var timer = new Stopwatch();
	var numberIgnored = 0;
	var numberProcessed = 0;
	foreach (var entry in diseases.EnumerateObject())
	{
		var disease = entry.Value.Deserialize<Disease>();
		disease.Id = entry.Name;
		
		timer.Restart();
		var data_subfolder = @$"{settings.SourceFolder}\{disease.Name}\{settings.SourceYear}";
		var disease_root_folder = Path.Combine(diseases_root_folder, data_subfolder);
		var disease_data_folders = new List<DiseaseFolder>(){
			new DiseaseFolder(new DirectoryInfo(disease_root_folder)),
		};

		foreach (var item in disease.InputFolders)
		{
			var folder = item.Value;
			if (!string.IsNullOrWhiteSpace(folder))
			{
				var folderInfo = new DirectoryInfo(Path.Combine(disease_root_folder, folder));
				var subfolder = new DiseaseFolder(folderInfo);
				if (item.Key.Equals("mortality", StringComparison.OrdinalIgnoreCase))
				{
					subfolder = new DiseaseFolder(folderInfo,"*", "mortality");
				}
				
				disease_data_folders.Add(subfolder);
			}
		}
		
		// Configure the disease processed output folders - disease.Id
		var output_subfolder_name = @$"{settings.OutputSubFolder}\{disease.Name}"; 
		var diseases_output_folder = new DirectoryInfo(Path.Combine(diseases_root_folder, output_subfolder_name));
		try
		{
			if (diseases_output_folder.Exists)
			{
				if (!disease.Overwrite)
				{
					numberIgnored++;
					Console.WriteLine("# Ignoring: {0}, overwriting is disabled.", disease.Id);
					continue;
				}
				
				diseases_output_folder.Delete(true);
			}
			
			numberProcessed++;
			Console.WriteLine("\n# Processing: {0} in folder: {1} ...", disease.Id, disease_root_folder);
			diseases_output_folder.Create();
			
			// Process IHME disease data file
			var result = ProcessIHMEDiseaseFolder(disease_data_folders, locations, settings.MaxAge, settings.UnitsMultiplier);
			Console.WriteLine("\nResults summary:\n{0}", result.ToString());

			// Create counties with filled age gaps and apply smoothing function!!
			var countries = CreateCountriesDatasetWithFilledAgeGaps(result, locations);
			var incomplete = ApplySmoothingToMeasuresInplace(countries, result.Measures, settings.SmoothingTimes);
			
			// Write data to CSV file
			WriteIHMEDiseaseDefinitionToCsv(countries, incomplete, diseases_output_folder);
			
			timer.Stop();
			Console.WriteLine("# Completed: {0} in {1} seconds.\n", disease.Id, timer.Elapsed.TotalSeconds);			
		}
		catch (Exception ex)
		{
			Console.WriteLine("# Failed to process: {0}, cause: {1}", disease.Id, ex.Message);
			throw; // Stop processing, remove to continue to next disease.
		}
	}
	
	Console.WriteLine("\n# Processed {0} and ignored {1} diseases respectively.", numberProcessed, numberIgnored);
}

public class Disease
{
	[JsonPropertyName("id")]
	public string Id { get; set; }
	
	[JsonPropertyName("overwrite")]
	public bool Overwrite { get; set; }
	
	[JsonPropertyName("folder_name")]
	public string Name { get; set; }
	
	[JsonPropertyName("input_folders")]
	public Dictionary<string, string> InputFolders { get; set; }
}

private static void ConvertOldFilesTo2019(DirectoryInfo sourceFolder, DirectoryInfo ouputFolder)
{
	if (!ouputFolder.Exists)
	{
		ouputFolder.Create();
	}
	
	var sourceFiles = sourceFolder.GetFiles("*.csv");
	foreach (var fileInfo in sourceFiles)
	{
		var outputFileName = Path.Combine(ouputFolder.FullName, fileInfo.Name);
		using(var writer = new StreamWriter(outputFileName))
		using(var reader = fileInfo.OpenText())
		{
			// File header
			var line = reader.ReadLine();
			writer.WriteLine(line);
			while ((line = reader.ReadLine()) != null)
			{
				var outline = line.Replace("2017","2019"); 
				writer.WriteLine(outline);
			}
		}
	}
}

private static IReadOnlyDictionary<int, List<IHMEOutputRow>> CreateCountriesDatasetWithFilledAgeGaps(
ResultInfo results, IReadOnlyDictionary<int, Location> locations)
{
	var timer = new Stopwatch();
	timer.Start();
	
	Console.WriteLine("Creating country specific datasets...");
	var summary = new SortedDictionary<int, List<IHMEOutputRow>>();
	var is_filled = false;
	var numberOfMetrics = results.Measures.Count;
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
		if (!summary.ContainsKey(loc.IsoCode))
		{
			summary.Add(loc.IsoCode, new List<IHMEOutputRow>());
		}

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
	}

	timer.Stop();
	Console.WriteLine("Completed: {0} countries, elapsed time: {1} seconds", summary.Count, timer.Elapsed.TotalSeconds);
	return summary;
}

private static void WriteIHMEDiseaseDefinitionToCsv(IReadOnlyDictionary<int, List<IHMEOutputRow>> countries,
HashSet<int> toIgnore, DirectoryInfo ouputFolderInfo)
{
	var timer = new Stopwatch();
	timer.Start();
	var numberOfCountries = 0;
	Console.WriteLine("\nWrite processed disease data to output files ...");
	try
	{
		foreach (var entry in countries)
		{
			if (toIgnore.Contains(entry.Key))
			{
				continue;
			}
			
			// Create new file stream
			numberOfCountries++;
			var location_file = Path.Combine(ouputFolderInfo.FullName, $"D{entry.Key}.csv");
			using (var sw = new StreamWriter(location_file, true, Encoding.UTF8))
			{
				sw.WriteLine(IHMEOutputRow.CsvHeader());
				foreach (var row in entry.Value)
				{
					sw.WriteLine(row.ToCsv());
				}
			}
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
		Console.WriteLine("Completed, {0} of {1} files created, elapsed time: {2} seconds",
		numberOfCountries, countries.Count, timer.Elapsed.TotalSeconds);
	}
}

private static HashSet<int> ApplySmoothingToMeasuresInplaceLocal(
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
					SmoothDataInplaceLocal(smoothing_times, smoothMale);
					SmoothDataInplaceLocal(smoothing_times, smoothFemale);
				}
				else 
				{
					countryMissingData = true;
					Console.WriteLine("No data for measure: {0} in country: {1}.", measure.Value, country_key);
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

public static void SmoothDataInplaceLocal(int times, Dictionary<int, double> data)
{
	var start_offset = data.First().Key;
	var count = data.Count + start_offset;
	var count_minus_one = count - 1;
	var keys = new List<int>(count);
	var working = new double[count];
	foreach (var item in data)
	{
		keys.Add(item.Key);
		working[item.Key] = item.Value;
	}

	if (data.Count > 2)
	{
		const double divisor = 3.0;
		for (var j = 0; j < times; j++)
		{
			var tmp = Array.ConvertAll(working, s => s);
			for (var i = 0; i < count; i++)
			{
				if (i == 0)
				{
					working[i] = (2.0 * tmp[i] + tmp[i + 1]) / divisor;
				}
				else if (i == count_minus_one)
				{
					working[i] = (tmp[i - 1] + tmp[i] * 2.0) / divisor;
				}
				else
				{
					working[i] = (tmp[i - 1] + tmp[i] + tmp[i + 1]) / divisor;
				}
			}
		}
	}

	var index = start_offset;
	foreach (var key in keys)
	{
		data[key] = working[index];
		index++;
	}
}


