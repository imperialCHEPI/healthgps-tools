<Query Kind="Program">
  <Namespace>System.Globalization</Namespace>
</Query>

#load "Process_common.linq"

void Main()
{
	var timer = new Stopwatch();
	timer.Start();
	
	var data_root_folder = @"C:\Work\Data";
	var countries_file = Path.Combine(data_root_folder, "ISO3166-1-Countries.csv");
	Console.WriteLine("*** Loading ISO 3166-1 Countries. ***\n");

	var countries = LoadCountries(countries_file);
	Console.WriteLine("There are {0} countries in file:", countries.Count);

	Console.WriteLine("\n*** Processing UN Population Database data. ***\n");
	var UN_DB_Folder = Path.Combine(data_root_folder, "UNDB2022");
	var populationFolder = new DirectoryInfo(Path.Combine(UN_DB_Folder, "Population"));

	string[] files = Directory.GetFiles(UN_DB_Folder, "*.csv");
	Console.WriteLine("The number of *.csv files in {0} is {1}.", UN_DB_Folder, files.Length);
	foreach (string dir in files)
	{
		Console.WriteLine(dir);
	}

	// 1. Clear ouput folder before we start
	if (!populationFolder.Exists)
	{
		populationFolder.Create();
	}
	else
	{
		foreach (var file in populationFolder.GetFiles("*.csv"))
		{
			file.Delete();
		}
	}

	// 2. Split historic file per country - population unit conversion from thousands
	var unit_multiplier = 1000.0f;
	//var population_file = Path.Combine(UN_DB_Folder, "WPP2019_PopulationBySingleAgeSex_1950-2019.csv");
	var population_file = Path.Combine(UN_DB_Folder, "WPP2022_PopulationBySingleAgeSex_Medium_1950-2021.csv");
	var fileIdCache = SplitPopulationFileByCountry(countries, population_file, populationFolder, null, unit_multiplier);

	// 3. Split forecast file per country - population unit conversion from thousands
	//population_file = Path.Combine(UN_DB_Folder, "WPP2019_PopulationBySingleAgeSex_2020-2100.csv");
	population_file = Path.Combine(UN_DB_Folder, "WPP2022_PopulationBySingleAgeSex_Medium_2022-2100.csv");

	_ = SplitPopulationFileByCountry(countries, population_file, populationFolder, fileIdCache, unit_multiplier);

	// Possible improvments
	// - Create a struct to represent each file row to load in mmeory (done)
	// - Index the file by gender and age group to speed up search
	// - Create a data base schema to store the data with version.

	Console.WriteLine("\n*** Create development datasets. ***\n");
	// Create development datasets for France, UK and Portugal
	var dev_countries = new List<string>
	{
		Path.Combine(populationFolder.FullName, "P250.csv"),
		Path.Combine(populationFolder.FullName, "P620.csv"),
		Path.Combine(populationFolder.FullName, "P826.csv")
	};

	CreateDevelopmentDatasets(dev_countries, 2010, 2030);

	timer.Stop();
	Console.WriteLine("\nScript total elapsed time: {0:N3} seconds.", timer.Elapsed.TotalSeconds);
}

public class Population12
{
	public int LocID { get; set; }
	public string Location { get; set; }
	public int VarID { get; set; }
	public string Variant { get; set; }
	public int Time { get; set; }
	public float MidPeriod { get; set; }
	public int AgeGrp { get; set; }
	public int AgeGrpStart { get; set; }
	public int AgeGrpSpan { get; set; }
	public float PopMale { get; set; }
	public float PopFemale { get; set; }
	public float PopTotal { get; set; }

	public override string ToString()
	{
		return $"{LocID},{Location},{VarID},{Variant},{Time},{MidPeriod},{AgeGrp},{AgeGrpStart},{AgeGrpSpan},{PopMale},{PopFemale},{PopTotal}";
	}

	public virtual string FileHeader()
	{
		return "LocID,Location,VarID,Variant,Time,MidPeriod,AgeGrp,AgeGrpStart,AgeGrpSpan,PopMale,PopFemale,PopTotal";
	}
}

public class Population20 : Population12
{
	public int SortOrder { get; set; }
	public string Notes { get; set; }
	public string ISO3_code { get; set; }
	public string ISO2_code { get; set; }
	public string SDMX_code { get; set; }
	public int LocTypeID { get; set; }
	public string LocTypeName { get; set; }
	public int ParentID { get; set; }

	public override string ToString()
	{
		return $"{SortOrder},{LocID},{Notes},{ISO3_code},{ISO2_code},{SDMX_code},{LocTypeID},{LocTypeName},{ParentID}," +
			   $"{Location},{VarID},{Variant},{Time},{MidPeriod},{AgeGrp},{AgeGrpStart},{AgeGrpSpan},{PopMale},{PopFemale},{PopTotal}";
	}

	public override string FileHeader()
	{
		return "SortOrder,LocID,Notes,ISO3_code,ISO2_code,SDMX_code,LocTypeID,LocTypeName,ParentID," +
			   "Location,VarID,Variant,Time,MidPeriod,AgeGrp,AgeGrpStart,AgeGrpSpan,PopMale,PopFemale,PopTotal";
	}
}

public string BuildFileHeader(int fieldsNumber)
{
	if (fieldsNumber == 12)
	{
		return new Population12().FileHeader();
	}
	else if (fieldsNumber == 20)
	{
		return new Population20().FileHeader();
	}

	throw new InvalidDataException($"Unknown population file format with : {fieldsNumber} fields.");
}

public Population12 BuildRow(string line)
{
	return BuildRow(line, 1.0f);
}

public static Population12 BuildRow(string line, float unit_multiplier)
{
	string[] fields = null;
	if (line.Contains("\""))
	{
		fields = SplitCsv(line);
	}
	else
	{
		fields = line.Split(",");
	}

	try
	{
		if (fields.Length == 12)
		{
			return BuildRowWith12Fields(fields, unit_multiplier);
		}
		else if (fields.Length == 20)
		{
			return BuildRowWith20Fields(fields, unit_multiplier);
		}
	}
	catch (Exception ex)
	{
		Console.WriteLine($"Invalid population line format: {line}, {ex.Message}");
		throw;
	}

	throw new InvalidDataException($"Invalid population line format: {line}, must have 12 or 20 fields.");
}

public static Population12 BuildRowWith12Fields(string[] fields, float unit_multiplier)
{
	// AgeGrp 100+
	if (fields[6].Contains("+"))
	{
		var content = fields[6];
		fields[6] = content.Substring(0, content.Length - 1);
	}

	return new Population12
	{
		LocID = int.Parse(fields[0].Trim(), CultureInfo.InvariantCulture),
		Location = fields[1].Trim(),
		VarID = int.Parse(fields[2], CultureInfo.InvariantCulture),
		Variant = fields[3].Trim(),
		Time = int.Parse(fields[4].Trim(), CultureInfo.InvariantCulture),
		MidPeriod = float.Parse(fields[5].Trim(), CultureInfo.InvariantCulture),
		AgeGrp = int.Parse(fields[6].Trim(), CultureInfo.InvariantCulture),
		AgeGrpStart = int.Parse(fields[7].Trim(), CultureInfo.InvariantCulture),
		AgeGrpSpan = int.Parse(fields[8].Trim(), CultureInfo.InvariantCulture),
		PopMale = float.Parse(fields[9].Trim(), CultureInfo.InvariantCulture) * unit_multiplier,
		PopFemale = float.Parse(fields[10].Trim(), CultureInfo.InvariantCulture) * unit_multiplier,
		PopTotal = float.Parse(fields[11].Trim(), CultureInfo.InvariantCulture) * unit_multiplier,
	};
}

public static Population20 BuildRowWith20Fields(string[] fields, float unit_multiplier)
{
	// AgeGrp 100+
	if (fields[14].Contains("+"))
	{
		var content = fields[14];
		fields[14] = content.Substring(0, content.Length - 1);
	}
	
	return new Population20
	{
		SortOrder = int.Parse(fields[0].Trim(), CultureInfo.InvariantCulture),
		LocID = int.Parse(fields[1].Trim(), CultureInfo.InvariantCulture),
		Notes = fields[2].Trim(),
		ISO3_code = fields[3].Trim(),
		ISO2_code = fields[4].Trim(),
		SDMX_code = fields[5].Trim(),
		LocTypeID = int.Parse(fields[6].Trim(), CultureInfo.InvariantCulture),
		LocTypeName = fields[7].Trim(),
		ParentID = int.Parse(fields[8].Trim(), CultureInfo.InvariantCulture),
		Location = fields[9].Trim(),
		VarID = int.Parse(fields[10], CultureInfo.InvariantCulture),
		Variant = fields[11].Trim(),
		Time = int.Parse(fields[12].Trim(), CultureInfo.InvariantCulture),
		MidPeriod = float.Parse(fields[13].Trim(), CultureInfo.InvariantCulture),
		AgeGrp = int.Parse(fields[14].Trim(), CultureInfo.InvariantCulture),
		AgeGrpStart = int.Parse(fields[15].Trim(), CultureInfo.InvariantCulture),
		AgeGrpSpan = int.Parse(fields[16].Trim(), CultureInfo.InvariantCulture),
		PopMale = float.Parse(fields[17].Trim(), CultureInfo.InvariantCulture) * unit_multiplier,
		PopFemale = float.Parse(fields[18].Trim(), CultureInfo.InvariantCulture) * unit_multiplier,
		PopTotal = float.Parse(fields[19].Trim(), CultureInfo.InvariantCulture) * unit_multiplier,
	};
}

IReadOnlyDictionary<int, int> SplitPopulationFileByCountry(
	IReadOnlyDictionary<int, Country> countries,
	string fileName,
	DirectoryInfo outputFolder,
	IReadOnlyDictionary<int, int> fileIdCache,
	float unit_multiplier)
{
	// Create / decode the file Ids cache
	Dictionary<int, int> fileIds = null;
	if (fileIdCache == null)
	{
		fileIds = new Dictionary<int, int>();
	}
	else
	{
		fileIds = fileIdCache.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
	}

	if (!File.Exists(fileName))
	{
		Console.WriteLine("Population file {0} not found.", fileName);
		return fileIds;
	}

	Console.WriteLine("\nProcessing population file {0} ... \n", fileName);
	var timer = new Stopwatch();
	timer.Start();
	using (var reader = new StreamReader(fileName, Encoding.UTF8))
	{
		string line;
		var fieldsNumber = 0;
		if ((line = reader.ReadLine()) == null)
		{
			Console.WriteLine("File {0} is empty.", fileName);
			return fileIds;
		}

		fieldsNumber = line.Split(",").Length;
		if (fieldsNumber != 12 && fieldsNumber != 20)
		{
			throw new InvalidDataException($"Invalid population file format: {line}, must have 12 or 20 fields.");
		}

		var file_header = BuildFileHeader(fieldsNumber);
		var code_idx_start = 0;
		var code_idx_end = -1;
		var code_value = 0;
		var country_lines = 0;
		var country_count = 0;
		var country_code = 0;
		var country_file = string.Empty;
		var is_newfile = false;
		var new_name_format = "{0,-5} Creating country file: {1,-" + Path.Combine(outputFolder.FullName, $"PXXX.csv").Length + "} ...";
		var old_name_format = new_name_format.Replace("Creating", "Updating");

		StreamWriter sw = null;
		try
		{
			while ((line = reader.ReadLine()) != null)
			{
				code_idx_end = line.IndexOf(",");
				if (fieldsNumber > 12)
				{
					code_idx_start = code_idx_end + 1;
					code_idx_end = line.IndexOf(",", code_idx_start);
				}

				code_value = int.Parse(line.Substring(code_idx_start, code_idx_end - code_idx_start));
				if (country_code != code_value)
				{
					if (!countries.ContainsKey(code_value))
					{
						continue; // Ignore non country codes
					}

					if (sw != null)
					{
						if (is_newfile)
						{
							is_newfile = false;
						}

						Console.WriteLine(" [OK] # lines = {0}.", country_lines);
						sw.Flush();
						sw.Dispose();
						sw = null;
						country_lines = 0;
					}

					country_code = code_value;
					country_file = Path.Combine(outputFolder.FullName, $"P{country_code}.csv");

					if (!File.Exists(country_file))
					{
						is_newfile = true;
						country_count++;
					}

					sw = new StreamWriter(country_file, true, Encoding.UTF8);
					if (is_newfile)
					{
						sw.WriteLine(file_header);
						fileIds.Add(code_value, country_count);
						Console.Write(new_name_format, country_count, country_file);
					}                                                
					else
					{
						Console.Write(old_name_format, fileIds[code_value], country_file);
					}
				}

				sw.WriteLine(BuildRow(line, unit_multiplier).ToString());
				country_lines++;
			}

			// Last country
			Console.WriteLine(" [OK] # lines = {0}.", country_lines);
		}
		catch (Exception ex)
		{
			Console.WriteLine("Error parsing file: {0}, cause: {1}", fileName, ex.Message);
			Console.WriteLine(ex);
		}
		finally
		{
			timer.Stop();
			if (sw != null)
			{
				sw.Flush();
				sw.Close();
				sw.Dispose();
			}

			Console.WriteLine(
			"\nCreated: {0} country files, elapsed time: {1} seconds.",
			country_count,
			timer.Elapsed.TotalSeconds);
		}

		return fileIds;
	}
}

void CreateDevelopmentDatasets(List<string> files, int minTime, int maxTime)
{
	var timer = new Stopwatch();
	timer.Start();

	foreach (var fileName in files)
	{
		if (!File.Exists(fileName))
		{
			Console.WriteLine("Population file {0} not found.", fileName);
			continue;
		}

		var devFileExt = Path.GetExtension(fileName);
		var devFileName = fileName.Replace($"{devFileExt}", $"_dev{devFileExt}");

		Console.Write("Processing population file {0} to {1} ... \n", fileName, devFileName);
		using (var writer = new StreamWriter(devFileName, false, Encoding.UTF8))
		using (var reader = new StreamReader(fileName, Encoding.UTF8))
		{
			string line;
			if ((line = reader.ReadLine()) == null)
			{
				Console.WriteLine("File {0} is empty.", fileName);
				continue;
			}

			writer.WriteLine(line);
			while ((line = reader.ReadLine()) != null)
			{
				var pop = BuildRow(line);
				if (minTime <= pop.Time && pop.Time <= maxTime)
				{
					writer.WriteLine(line);
				}
			}
		}
	}

	timer.Stop();
	Console.WriteLine("\nCompleted, elapsed time: {0} seconds.", timer.Elapsed.TotalSeconds);
}