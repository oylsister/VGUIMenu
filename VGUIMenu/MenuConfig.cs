using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace VGUIMenu;

internal sealed class MenuConfig
{
    public const string FileName = "vguimenu.jsonc";

    /// <summary>Compiled Panorama layout. It must be shipped to clients (workshop addon).</summary>
    public string LayoutPath { get; set; } = "panorama/layout/custom_game/vguimenu.vxml_c";

    /// <summary>Lines longer than this wrap to a new line.</summary>
    public int WrapLength { get; set; } = 32;

    public SoundConfig Sounds { get; set; } = new ();

    public LabelConfig Labels { get; set; } = new ();

    /// <summary>Registers ms_vguimenu_test (!vguimenu_test) to preview menus.</summary>
    public bool EnableTestCommand { get; set; } = true;

    internal sealed class SoundConfig
    {
        public bool   Enabled  { get; set; } = true;
        public string Select   { get; set; } = "UIPanorama.submenu_select";
        public string Exit     { get; set; } = "UIPanorama.submenu_dropdown_select";
        public string ExitBack { get; set; } = "UIPanorama.submenu_dropdown_select";
    }

    internal sealed class LabelConfig
    {
        /// <summary>Slot 7 when there is a previous page.</summary>
        public string Previous { get; set; } = "Back";

        /// <summary>Slot 7 when the menu has <c>ExitBackButton</c> and is on its first page.</summary>
        public string ExitBack { get; set; } = "Back";

        public string Next { get; set; } = "Next";
        public string Exit { get; set; } = "Exit";
    }

    private static readonly JsonSerializerOptions Options = new ()
    {
        WriteIndented               = true,
        ReadCommentHandling         = JsonCommentHandling.Skip,
        AllowTrailingCommas         = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.Never,
    };

    public static MenuConfig Load(string sharpPath, ILogger logger)
    {
        var path = Path.Combine(sharpPath, "configs", FileName);

        try
        {
            if (!File.Exists(path))
            {
                var config = new MenuConfig();
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(config, Options));
                logger.LogInformation("Created default config at {Path}", path);

                return config;
            }

            var loaded = JsonSerializer.Deserialize<MenuConfig>(File.ReadAllText(path), Options) ?? new MenuConfig();

            if (loaded.WrapLength < 8)
            {
                logger.LogWarning("WrapLength {Value} is too small, using 32", loaded.WrapLength);
                loaded.WrapLength = 32;
            }

            return loaded;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to load {Path}, using defaults", path);

            return new MenuConfig();
        }
    }
}
