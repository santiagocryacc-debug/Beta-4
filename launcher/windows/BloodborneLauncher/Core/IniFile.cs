using System.IO;

namespace BloodborneLauncher.Core
{
    /// <summary>
    /// bbport.ini: the port's own settings (upscaler, preset, controls, ...), also edited by the
    /// in-game menu. The edited keys are rewritten in place, missing ones
    /// appended, comments and unknown keys kept, and a key set to null is removed (a control
    /// binding back to its default).
    /// </summary>
    public sealed class IniFile
    {
        public static readonly (string Key, string Value)[] Defaults =
        {
            ("upscaler", "fsr4"),
            ("preset", "4"),
            ("sharpen", "1"),
            ("sharpness", "0.50"),
            ("object_motion", "1"),
            ("show_fps", "1"),
            ("output_res", "1920x1080"),
            ("model_lod", "0"),
            ("live_resolution", "0"),
        };

        private readonly Dictionary<string, string?> _values = new(StringComparer.Ordinal);
        private List<string> _lines = new();

        public void Load()
        {
            _values.Clear();
            foreach (var (key, value) in Defaults)
                _values[key] = value;
            foreach (var effect in Choices.Effects)
                _values[effect.Key] = effect.Default ? "1" : "0";

            _lines = new List<string>();
            try
            {
                if (File.Exists(Paths.IniPath))
                    _lines = File.ReadAllLines(Paths.IniPath).ToList();
            }
            catch
            {
                // Unreadable: start from the defaults.
            }

            foreach (var line in _lines)
            {
                if (!line.Contains('=') || line.TrimStart().StartsWith('#'))
                    continue;
                var at = line.IndexOf('=');
                _values[line[..at].Trim()] = line[(at + 1)..].Trim();
            }
        }

        public void Save()
        {
            var written = new HashSet<string>(StringComparer.Ordinal);
            var output = new List<string>();
            foreach (var line in _lines)
            {
                if (line.Contains('=') && !line.TrimStart().StartsWith('#'))
                {
                    var key = line[..line.IndexOf('=')].Trim();
                    if (_values.TryGetValue(key, out var value))
                    {
                        if (value != null && written.Add(key))
                            output.Add($"{key}={value}");
                        written.Add(key);
                        continue;
                    }
                }
                output.Add(line);
            }

            if (_lines.Count == 0)
                output.Add("# bbport settings (in-game menu: Insert / L3+R3)");
            foreach (var (key, value) in _values)
                if (!written.Contains(key) && value != null)
                    output.Add($"{key}={value}");

            Directory.CreateDirectory(Path.GetDirectoryName(Paths.IniPath)!);
            // Keep the last known-good config before changing it. The game and this launcher share
            // bbport.ini, so an accidental UI change must be recoverable.
            if (File.Exists(Paths.IniPath))
            {
                var backup = Paths.IniPath + ".bak";
                try { File.Copy(Paths.IniPath, backup, overwrite: true); }
                catch (IOException) { /* Continue only if the main file remains writable. */ }
                catch (UnauthorizedAccessException) { /* File.WriteAllText below reports the actual save failure. */ }
            }
            var temp = Paths.IniPath + ".tmp";
            File.WriteAllText(temp, string.Join("\n", output) + "\n");
            if (File.Exists(Paths.IniPath))
                File.Replace(temp, Paths.IniPath, null);
            else
                File.Move(temp, Paths.IniPath);
            _lines = output;
        }

        /// <summary>The value, or null when the key is absent (e.g. a binding left at its default).</summary>
        public string? Raw(string key) => _values.TryGetValue(key, out var v) ? v : null;

        public string Get(string key, string fallback = "") => Raw(key) ?? fallback;

        public bool Flag(string key) => Get(key) == "1";

        public void Set(string key, string? value) => _values[key] = value;

        /// <summary>Every key with a value (shared settings).</summary>
        public IEnumerable<KeyValuePair<string, string>> Values =>
            _values.Where(kv => kv.Value != null).Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value!));
    }
}
