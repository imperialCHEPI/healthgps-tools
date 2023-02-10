using System;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace HealthGPS.Tools;

/// <summary>
/// Health-GPS supporting tools CLI definition.
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
        Console.WriteLine("{1}# HealthGPS Supporting Tools v{0} #{1}", appVersion, Environment.NewLine);
        Console.ResetColor();

        var returnCode = 0;
        var rootCommand = new RootCommand($"Health-GPS Tools CLI options.");

        var fileOption = CreateFileOption();
        rootCommand.AddGlobalOption(fileOption);

        var outputCommand = new Command("output", "Process the simulation output files.");
        rootCommand.AddCommand(outputCommand);
        outputCommand.SetHandler((file) =>
        {
            returnCode = RunOutputCommand(file);
        }, fileOption);

        var timer = new Stopwatch();
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
        Console.WriteLine("# Goodbye from Health-GPS Tools #");
        Console.ResetColor();
        Console.WriteLine();
        return returnCode;
    }

    /// <summary>
    /// Run the Simulation Output files processing command.
    /// </summary>
    /// <param name="optionsFile">The output tool options file.</param>
    private static int RunOutputCommand(FileInfo optionsFile)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Starting Health-GPS output processing ...");
        Console.ResetColor();
        Console.WriteLine();
        try
        {
            var commandTool = new OutputTool(optionsFile);
            commandTool.Execute();
            Console.WriteLine(commandTool.Logger);
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
    /// Creates the global file argument for all commands.
    /// </summary>
    /// <returns>The file command option</returns>
    private static Option<FileInfo> CreateFileOption()
    {
        var option = new Option<FileInfo>(
            name: "--file",
            description: "Configuration file for the command.",
            isDefault: true,
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
