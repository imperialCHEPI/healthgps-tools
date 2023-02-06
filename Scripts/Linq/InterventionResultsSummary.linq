<Query Kind="Program">
  <Output>DataGrids</Output>
</Query>

void Main()
{
	Console.WriteLine("*** Creating Simulation Results Summaries ***");
	Console.WriteLine();

	var ageRange = "0_43";
	var intervention = "FoodLabelling";
	var rootFolder = new DirectoryInfo("C:/HealthGPS/Results");
	var countries = new List<string> { "Finland", "France", "Italy", "Portugal", "Slovenia", "Spain", "United Kingdom" };
	var resultFiles = new List<string> { "Baseline", "Intervention", "Difference", "PercentDifference" };
	var extractYears = new List<int> { 2050 };
	var hceFilePattern = $"*_HCE_Prediction_summary_age_{ageRange}.csv";
	if (!rootFolder.Exists)
	{
		throw new DirectoryNotFoundException($"Root folder: {rootFolder.FullName} not found.");
	}

	var hceOutputFile = new FileInfo(Path.Combine(rootFolder.FullName, $"{intervention}_HCE_Prediction_age_{ageRange}.csv"));
	if (hceOutputFile.Exists)
	{
		hceOutputFile.Delete();
	}

	var resultsOutputFile = new FileInfo(Path.Combine(rootFolder.FullName, $"{intervention}_Results_age_{ageRange}.csv"));
	if (resultsOutputFile.Exists)
	{
		resultsOutputFile.Delete();
	}
	
	Console.WriteLine($"- Measure output file: {resultsOutputFile.FullName}");
	Console.WriteLine($"- HCE output file....: {hceOutputFile.FullName}");
	Console.WriteLine();
	
	var timer = new Stopwatch();
	timer.Start();
	foreach (var country in countries)
	{
		try
		{
			var dataFolder = new DirectoryInfo(Path.Combine(rootFolder.FullName, country, "Summary"));//, $"{intervention}5KRuns"));
			if (!dataFolder.Exists)
			{
				throw new DirectoryNotFoundException($"Country results folder: {dataFolder.FullName} not found.");
			}
			
			Console.WriteLine($"- Source data folder.: {dataFolder.FullName}");
			var hceSummaryFile = dataFolder.GetFiles(hceFilePattern).FirstOrDefault();
			if (hceSummaryFile == null)
			{
				throw new FileNotFoundException($"HCE summary file not found in folder: {dataFolder.FullName}.");
			}

			ProcessHCESummary(hceOutputFile, country, hceSummaryFile);
			
			foreach (var year in extractYears)
			{
				ProcessMeanSummary(dataFolder, resultFiles, country, intervention, year, resultsOutputFile, ageRange, "Mean");
				ProcessMeanSummary(dataFolder, resultFiles, country, intervention, year, resultsOutputFile, ageRange, "StDev");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"ERROR: {ex.Message}");
		}
	}

	timer.Stop();
	Console.WriteLine();
	Console.WriteLine("Completed, {0} countries, elapsed time: {1} seconds", countries.Count, timer.Elapsed.TotalSeconds);
}

private static void ProcessMeanSummary(DirectoryInfo dataFolder, IReadOnlyList<string> files,
					string country, string intervention, int targetYear, FileInfo outputFile, string ageRange, string stats)
{
	var writeFileHeader = !File.Exists(outputFile.FullName);
	using (var sw = new StreamWriter(outputFile.FullName, true, Encoding.UTF8))
	{
		foreach (var measure in files)
		{
			var dataFile = dataFolder.GetFiles($"{measure}*_age_{ageRange}_{stats}.csv").FirstOrDefault();
			if (dataFile == null)
			{
				throw new FileNotFoundException($"{country} {measure} summary file not found in folder: {dataFolder.FullName}.");
			}
			
			using (var sr = dataFile.OpenText())
			{
				var line = sr.ReadLine();
				if (writeFileHeader)
				{
					sw.WriteLine($"country,intervention,measure,stats,{line}");
					writeFileHeader = false;
				}

				while ((line = sr.ReadLine()) != null)
				{
					var rowYear = int.Parse(line.Substring(0, line.IndexOf(",")));
					if (rowYear != targetYear)
					{
						continue;
					}

					sw.WriteLine($"{country},{intervention},{measure},{stats},{line}");
				}
			}
		}
	}
}

private static void ProcessHCESummary(FileInfo hceOutputFile, string country, FileInfo hceSummaryFile)
{
	var writeFileHeader = !File.Exists(hceOutputFile.FullName);
	using (var sw = new StreamWriter(hceOutputFile.FullName, true, Encoding.UTF8))
	{
		using (var sr = hceSummaryFile.OpenText())
		{
			var line = sr.ReadLine();
			if (writeFileHeader)
			{
				sw.WriteLine($"country,{line}");
			}

			while ((line = sr.ReadLine()) != null)
			{
				sw.WriteLine($"{country},{line}");
			}
		}
	}
}
