using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace Updater
{
    class Program
    {
        static void Main(string[] args)
        {
            // Args: pid, zipPath, appDirectory, executablePath, logFilePath, updaterCachePath, preserveConfig
            if (args.Length < 7)
            {
                return;
            }

            var pid = int.Parse(args[0]);
            var zipPath = args[1];
            var appDirectory = args[2];
            var executablePath = args[3];
            var logFilePath = args[4];
            var updaterCachePath = args[5];
            var preserveConfig = bool.Parse(args[6]); // New argument

            using (var logWriter = new StreamWriter(logFilePath, true))
            {
                try
                {
                    logWriter.WriteLine("--- Updater Log ---");
                    logWriter.WriteLine($"Updater started at {DateTime.Now}");

                    // 1. Wait for the main application to exit
                    try
                    {
                        var mainProcess = Process.GetProcessById(pid);
                        logWriter.WriteLine($"Waiting for main application process (PID: {pid}) to exit...");
                        mainProcess.WaitForExit();
                        logWriter.WriteLine("Main application process has exited.");
                    }
                    catch (ArgumentException)
                    {
                        // Process is already dead
                        logWriter.WriteLine("Main application process was not found (already exited).");
                    }

                    // Give the OS some time to release file locks
                    Thread.Sleep(2000);

                    // 2. Extract the contents of the downloaded .zip file
                    var extractionPath = Path.Combine(updaterCachePath, "extracted_update");
                    Directory.CreateDirectory(extractionPath);
                    logWriter.WriteLine($"Extracting update from {zipPath} to {extractionPath}...");
                    using (var archive = ArchiveFactory.Open(zipPath))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            if (!entry.IsDirectory)
                            {
                                string destinationPath = Path.Combine(extractionPath, entry.Key);
                                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                                entry.WriteToFile(destinationPath, new ExtractionOptions() { Overwrite = true, ExtractFullPath = true });
                            }
                        }
                    }
                    logWriter.WriteLine("Update extracted successfully.");

                    // 3. Handle config.json based on preserveConfig
                    var configPath = Path.Combine(appDirectory, "config.json");

                    if (!preserveConfig)
                    {
                        if (File.Exists(configPath))
                        {
                            logWriter.WriteLine("Deleting config.json (clean update)...");
                            File.Delete(configPath);
                        }
                    }

                    // 4. Copy the extracted files to the application's root directory
                    logWriter.WriteLine("Copying new files...");
                    foreach (var file in Directory.GetFiles(extractionPath, "*.*", SearchOption.AllDirectories))
                    {
                        var relativePath = file.Substring(extractionPath.Length + 1);
                        var destinationPath = Path.Combine(appDirectory, relativePath);
                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                        File.Copy(file, destinationPath, true);
                    }
                    logWriter.WriteLine("New files copied successfully.");

                    // 6. Delete the temporary extraction directory
                    logWriter.WriteLine("Deleting temporary extraction directory...");
                    Directory.Delete(extractionPath, true);

                    // 7. Delete the downloaded .zip file
                    logWriter.WriteLine("Deleting downloaded zip file...");
                    File.Delete(zipPath);

                    // 8. Restart the main application
                    logWriter.WriteLine("Restarting application...");
                    Process.Start(new ProcessStartInfo(executablePath)
                    {
                        UseShellExecute = false
                    });

                    logWriter.WriteLine("Update completed successfully.");
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine($"An error occurred during the update: {ex}");
                }
            }
        }
    }
}
