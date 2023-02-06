<Query Kind="Program">
  <Output>DataGrids</Output>
</Query>

void Main()
{
	var timer = new Stopwatch();
	timer.Start();
	Console.WriteLine($"Converting UN WPP datasets from v1 to v2 formats.\n");
	
	var rootSourceFolder = new DirectoryInfo(@"C:\Workspace\data\undb_2019_v1\");
	var rootOutputFolder = new DirectoryInfo(@"C:\Workspace\data\undb_2019_v2\");
	var datasetConverters = new List<ConverterInfo>
	{
		new ConverterInfo("population", "P*.csv", PopulationConverter),
		new ConverterInfo("mortality", "M*.csv", MortalityConverter),
		new ConverterInfo("indicators", "Pi*.csv", IndicatorConverter),
	};
	
	try
	{
		// 1. Create output folder
		if (rootOutputFolder.Exists)
		{
			rootOutputFolder.Delete(true);
		}

		rootOutputFolder.Create();

		// 2. Convert
		foreach (var info in datasetConverters)
		{
			Console.WriteLine($"# Converting dataset: {info.Identifier} ...");
			var sourceFolder = new DirectoryInfo(Path.Combine(rootSourceFolder.FullName, info.Identifier));
			var outputFolder = new DirectoryInfo(Path.Combine(rootOutputFolder.FullName, info.Identifier));
			var elapsed = ConvertDataset(sourceFolder, outputFolder, info.FilePattern, info.Function);
			Console.WriteLine("# Completed, elapsed time: {0:N3} seconds.\n", elapsed.TotalSeconds);
		}
	}
	catch (Exception ex)
	{
		Console.WriteLine("The process failed: {0}", ex.Message);
		throw;
	}
	
	timer.Stop();
	Console.WriteLine("Script total elapsed time: {0:N3} seconds.", timer.Elapsed.TotalSeconds);
}

public class ConverterInfo
{
	public ConverterInfo(string id, string fileFilter, Func<string,string> converter)
	{
		this.Identifier = id.ToLower();
		this.FilePattern = fileFilter;
		this.Function = converter;
	}
	
	public string Identifier {get;}
	public string FilePattern {get;}
	public Func<string,string> Function {get;}
}

private static TimeSpan ConvertDataset(DirectoryInfo source, DirectoryInfo output, string filePattern, Func<string,string> converter)
{
	var timer = new Stopwatch();
	timer.Start();
	if (!source.Exists)
	{
		throw new DirectoryNotFoundException($"Source dataset folder: {source.FullName} not found.");
	}
	
	if (!output.Exists)
	{
		output.Create();
	}
	
	var datasets = source.GetFiles(filePattern);
	Console.WriteLine($"> The dataset contains: {datasets.Length} files.");
	foreach (var file in datasets)
	{
		var outputFilename = Path.Combine(output.FullName, file.Name);
		using (var reader = file.OpenText())
		{
			var header = converter(reader.ReadLine());
			using (var writer = new StreamWriter(outputFilename, false, Encoding.UTF8))
			{
				writer.WriteLine(header);
				var line = string.Empty;
				while ((line = reader.ReadLine()) != null)
				{
					writer.WriteLine(line);
				}
			}
		}
	}

	timer.Stop();
	return timer.Elapsed;
}


private static string PopulationConverter(string fileHeader)
{
	// v1 - LocID,Location,VarID,Variant,Time,MidPeriod,AgeGrp,AgeGrpStart,AgeGrpSpan,PopMale,PopFemale,PopTotal
	// v2 - LocID,Location,VarID,Variant,Time,Age,PopMale,PopFemale,PopTotal
	return fileHeader.Replace(",AgeGrp,",",Age,", StringComparison.OrdinalIgnoreCase);
}

private static string MortalityConverter(string fileHeader)
{
	// v1 - LocID,Location,Variant,Time,TimeYear,AgeGrp,Age,DeathsMale,DeathsFemale,DeathsTotal
	// v2 - LocID,Location,VarID,Variant,Time,Age,DeathMale,DeathFemale,DeathTotal
	return fileHeader.Replace(",Time,",",Period,", StringComparison.OrdinalIgnoreCase)
					 .Replace(",TimeYear,",",Time,", StringComparison.OrdinalIgnoreCase)
					 .Replace(",Deaths",",Death", StringComparison.OrdinalIgnoreCase);
}

private static string IndicatorConverter(string fileHeader)
{
	// v1 - LocID,Location,VarID,Variant,Time,MidPeriod,TimeYear,Births,LEx,LExMale,LExFemale,Deaths,DeathsMale,DeathsFemale,NetMigrations,SRB
	// v2 - LocID,Location,VarID,Variant,Time,Births,SRB,LEx,LExMale,LExFemale,Deaths,DeathsMale,DeathsFemale,NetMigrations
	return fileHeader.Replace(",Time,", ",Period,", StringComparison.OrdinalIgnoreCase)
					 .Replace(",TimeYear,", ",Time,", StringComparison.OrdinalIgnoreCase);
}
