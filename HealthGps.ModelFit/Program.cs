using System;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using HealthGps.R;

namespace HealthGps.ModelFit
{
    /// <summary>
    /// Health-GPS user defined models and dataset fitting tool.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// The application entry point.
        /// </summary>
        /// <param name="args">The command line arguments. </param>
        /// <returns>The execution result, 0 for success, anything else for failure.</returns>
        static async Task<int> Main(string[] args)
        {
            var appVersion = GetAppVersion();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("{1}# Health-GPS Models Fitting Tool v{0} #{1}", appVersion, Environment.NewLine);
            Console.ResetColor();

            var returnCode = 0;
            var rootCommand = new RootCommand($"Health-GPS Models Fitting Tools CLI options.");

            var fileOption = CreateFileOption();
            rootCommand.AddGlobalOption(fileOption);

            var riskFactorCommand = new Command("risk-factor", "Fits a hierarchical risk factor model to data.");
            rootCommand.AddCommand(riskFactorCommand);
            riskFactorCommand.SetHandler((file) =>
            {
                returnCode = FitRiskFactorModel(file);
            }, fileOption);

            var timer = new Stopwatch();
            timer.Start();
            await rootCommand.InvokeAsync(args);
            timer.Stop();
            if (returnCode == 0)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Completed, elapsed time: {timer.Elapsed.TotalSeconds} seconds.");
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("# Goodbye from Health-GPS Models Fitting Tool #");
            Console.ResetColor();
            Console.WriteLine();

            return returnCode;
        }

        /// <summary>
        /// Creates the global file argument for all commands.
        /// </summary>
        /// <returns>The file command option</returns>
        private static Option<FileInfo> CreateFileOption()
        {
            var option = new Option<FileInfo>(
                name: "--file",
                description: "Configuration file for the command.",
                parseArgument: result =>
                {
                    if (result.Tokens.Count == 0)
                    {
                        result.ErrorMessage = $"Missing configuration file for command.";
                        return null;
                    }

                    string filePath = result.Tokens.Single().Value;
                    if (!File.Exists(filePath))
                    {
                        result.ErrorMessage = $"Configuration file: {filePath} does not exist.";
                        return null;
                    }

                    return new FileInfo(filePath);
                });

            option.AddAlias("-f");
            option.IsRequired = true;
            return option;
        }

        /// <summary>
        /// Fits the risk factor hierarchical model definition to a dataset.
        /// </summary>
        /// <param name="optionsFile">The risk factor model definition file.</param>
        /// <returns>The fitting result, 0 for success and 1 for failure.</returns>
        private static int FitRiskFactorModel(FileInfo optionsFile)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Fitting hierarchical risk factor model:");
            Console.ResetColor();
            Console.WriteLine(optionsFile.FullName);

            try
            {
                var definition = ConfigurationParser.LoadRiskFactorModel(optionsFile);
                var statsEngine = new RStatsProvider();

                var progress = new Progress<double>(percent =>
                {
                    Console.WriteLine(percent + "%");
                });

                var modelBuilder = new RiskFactorModelBuilder(statsEngine, definition);
                var riskModel = modelBuilder.Build(progress);

                var outputFolder = optionsFile.Directory;
                ConfigurationParser.WriteRiskFactorToJson(outputFolder, riskModel);
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(GetMessageFromException(ex));
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        /// <summary>
        /// Extract the exception source message
        /// </summary>
        /// <param name="ex">The exception to extract message.</param>
        /// <returns>The exception message.</returns>
        private static string GetMessageFromException(Exception ex)
        {
            if (ex == null) return "";
            if (ex.InnerException != null)
            {
                return GetMessageFromException(ex.InnerException);
            }

            return ex.Message;
        }

        /// <summary>
        /// Gets the application version
        /// </summary>
        /// <returns>The application version number</returns>
        private static string GetAppVersion()
        {
            var assembly = Assembly.GetEntryAssembly();
            if (assembly != null)
            {
                var versionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                if (versionAttribute != null)
                {
                    return versionAttribute.InformationalVersion;
                }
            }

            return "Unknown";
        }
    }
}
