using System;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace HealthGPS.Tools;

/// <summary>
/// Health-GPS supporting tools CLI definition.
/// </summary>
class Program
{
    /// <summary>
    /// The application entry point.
    /// </summary>
    /// <param name="args">The command line arguments. </param>
    /// <returns>The execution result, 0 for success, anything else for failure.</returns>
    static int Main(string[] args)
    {
        var appVersion = GetAppVersion();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("{1}# HealthGPS Supporting Tools v{0} #{1}", appVersion, Environment.NewLine);
        Console.ResetColor();

        var rootCommand = new RootCommand($"Health-GPS Tools CLI options.");

        var fileOption = CreateFileOption();
        rootCommand.AddGlobalOption(fileOption);

        var outputCommand = new Command("output", "Process the simulation output files.");
        rootCommand.AddCommand(outputCommand);
        outputCommand.SetHandler((file) =>
        {
            RunOutputCommand(file);
        }, fileOption);

        var timer = new Stopwatch();
        var returnValue = 0;
        try
        {
            timer.Start();
            returnValue = rootCommand.InvokeAsync(args).Result;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(GetMessageFromException(ex));
            Console.ResetColor();
            Console.WriteLine(ex.StackTrace);
        }
        finally
        {
            timer.Stop();
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Total elapsed time: {timer.Elapsed.TotalSeconds} seconds.");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("# Goodbye from Health-GPS Tools #");
            Console.ResetColor();
            Console.WriteLine();
        }

        return returnValue;
    }

    /// <summary>
    /// Run the Simulation Output files processing command.
    /// </summary>
    /// <param name="optionsFile">The output tool options file.</param>
    private static void RunOutputCommand(FileInfo optionsFile)
    {
        var commandTool = new OutputTool(optionsFile);
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Starting: {commandTool.Name} ...");
        Console.ResetColor();
        Console.WriteLine();
        commandTool.Execute();
        Console.WriteLine(commandTool.Logger);
    }

    /// <summary>
    /// Creates the global file argument for all commands.
    /// </summary>
    /// <returns>The file command option</returns>
    private static Option<FileInfo> CreateFileOption()
    {
        return new Option<FileInfo>(
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
