using System;
using System.Collections.Generic;
using System.IO;

namespace ContextReader
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Starting...");

            try
            {
                var path = args.Length > 0 ? args[0] : @"C:\Project\DbContext.cs";
                var configurationsDir = Path.Combine(Path.GetDirectoryName(path) ?? "C:", "Configurations");
                Directory.CreateDirectory(configurationsDir);
                
                var createdConfigurations = new List<string>();

                var lines = File.ReadAllLines(path);
                var newLines = new List<string>();

                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].TrimStart().StartsWith("modelBuilder.Entity<"))
                    {
                        var entityName = lines[i].Split('<', '>')[1];

                        var configurationLines = new List<string>();

                        for (int j = i + 1; j < lines.Length; j++)
                        {
                            var line = lines[j]
                                .Replace("entity.", "builder.")
                                .Replace("e => e", "i => i");

                            if (line.StartsWith("            });"))
                            {
                                i = j + 1;
                                break;
                            }

                            if (line.TrimStart().StartsWith(".HasName("))
                            {
                                configurationLines[^1] = configurationLines[^1] + line.TrimStart();
                            }
                            else
                            {
                                if (string.IsNullOrWhiteSpace(line) &&
                                    configurationLines[^1].TrimStart().StartsWith("builder.Property(") &&
                                    j + 1 < lines.Length && lines[j + 1].TrimStart().StartsWith("entity.Property("))
                                {

                                }
                                else if (string.IsNullOrWhiteSpace(line) &&
                                         configurationLines[^1].TrimStart().StartsWith("builder.HasIndex(") &&
                                         j + 1 < lines.Length && lines[j + 1].TrimStart().StartsWith("entity.HasIndex("))
                                {

                                }
                                else
                                {
                                    configurationLines.Add(line);
                                }
                            }
                        }

                        var configurationName = (entityName.EndsWith("s") ? entityName[..^1] : entityName).Replace("Entity", "") + "Configuration";
                        createdConfigurations.Add(configurationName);
                        File.WriteAllLines(Path.Combine(configurationsDir,"{configurationName}.cs"), ConfigurationFileLines(configurationLines, configurationName, entityName));
                        Console.WriteLine($"Created {configurationName}.cs");
                    }
                    else
                    {
                        newLines.Add(lines[i]);
                    }
                }

                File.WriteAllLines(path, newLines);

                var lines2 = File.ReadAllLines(path);
                var newLines2 = new List<string>();

                for (var i = 0; i < lines2.Length; i++)
                {
                    var l = lines2[i];
                    newLines2.Add(l);
                    if (i > 1 && l.TrimEnd().EndsWith("{") && lines2[i-1].TrimEnd().EndsWith("protected override void OnModelCreating(ModelBuilder modelBuilder)"))
                    {
                        foreach (var configurationFile in createdConfigurations)
                        {
                            newLines2.Add($"            modelBuilder.ApplyConfiguration(new {configurationFile}());");
                        }
                    }
                }

                File.WriteAllLines(path, newLines2);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }

            Console.WriteLine("Done.");
            Console.ReadKey();
        }

        private static List<string> ConfigurationFileLines(IEnumerable<string> configurationLines, string configurationName, string entityName)
        {
            var configurationFile = new List<string>
            {
                "using Microsoft.EntityFrameworkCore;",
                "using Microsoft.EntityFrameworkCore.Metadata.Builders;",
                "using DataAccess.Entities;",
                "",
                "namespace DataAccess.DbContext.Configurations",
                "{",
                "    /// <summary>",
                $"    /// Entity framework configurations for <see cref=\"{entityName}\"/>",
                "    /// </summary>",
                $"    internal class {configurationName} : IEntityTypeConfiguration<{entityName}>",
                "    {",
                $"        public void Configure(EntityTypeBuilder<{entityName}> builder)",
            };

            configurationFile.AddRange(configurationLines);

            configurationFile.Add("        }");
            configurationFile.Add("    }");
            configurationFile.Add("}");

            return configurationFile;
        }
    }
}
