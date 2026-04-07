using Com_ELF.Models;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace Com_ELF.Services
{
    public static class ElfSymbolReader
    {
        public static List<ElfSymbol> ReadObjectSymbols(string elfPath)
        {
            if (string.IsNullOrWhiteSpace(elfPath))
                throw new ArgumentException("ELF path is empty.", nameof(elfPath));

            if (!File.Exists(elfPath))
                throw new FileNotFoundException("ELF file not found.", elfPath);

            string objdumpPath = FindObjdumpPath();

            var symbols = new List<ElfSymbol>();

            var process = new ProcessStartInfo
            {
                FileName = objdumpPath,
                Arguments = $"-t \"{elfPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var proc = Process.Start(process))
            {
                if (proc == null)
                    throw new InvalidOperationException("Failed to start objdump.");

                string stdout = proc.StandardOutput.ReadToEnd();
                string stderr = proc.StandardError.ReadToEnd();

                proc.WaitForExit();

                if (proc.ExitCode != 0)
                    throw new InvalidOperationException(
                        $"objdump failed with exit code {proc.ExitCode}.{Environment.NewLine}{stderr}");

                foreach (string rawLine in stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string line = rawLine.Trim();

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    if (line.StartsWith("/"))
                        continue;

                    if (line.StartsWith("SYMBOL TABLE"))
                        continue;

                    // Beispiel:
                    // 3400087c g     O .bss 00000038 PrintVars
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length < 6)
                        continue;

                    if (!uint.TryParse(parts[0], System.Globalization.NumberStyles.HexNumber, null, out uint address))
                        continue;

                    // Nur Objekt-Symbole laden
                    // parts[2] ist bei objdump -t typischerweise "O" für Object
                    if (!string.Equals(parts[2], "O", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!uint.TryParse(parts[4], System.Globalization.NumberStyles.HexNumber, null, out uint size))
                        continue;

                    // Name beginnt hier bei Index 5, nicht 6
                    string name = string.Join(" ", parts.Skip(5));

                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    // Abschnittseinträge wie ".bss" oder ".data" nicht als Variable behandeln
                    if (name.StartsWith("."))
                        continue;

                    symbols.Add(new ElfSymbol
                    {
                        Name = name,
                        Address = address,
                        Size = size
                    });
                }
            }

            return symbols;
        }

        private static string FindObjdumpPath()
        {
            return @"C:\Users\Matthias\Infineon\Tools\mtb-gcc-arm-eabi\14.2.1\gcc\bin\arm-none-eabi-objdump.exe";
        }
    }
}