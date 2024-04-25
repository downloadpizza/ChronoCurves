using SFML.Graphics;
using SFML.Window;
using Tomlet.Attributes;

namespace ChronoCurves
{
    public class Config {
        [TomlProperty("keys")] public KeyConfig Keys { get; set; }
        [TomlProperty("visualization")] public VisualizationConfig Visualization { get; set; }
        [TomlProperty("axis")] public Dictionary<string, AxisConfig> Axis { get; set; }
    }

    public class AxisConfig {
        [TomlProperty("positive")] public HashSet<HashSet<Keyboard.Key>> PositiveKC { get; set;} = new HashSet<HashSet<Keyboard.Key>>();
        [TomlProperty("negative")] public HashSet<HashSet<Keyboard.Key>> NegativeKC { get; set;} = new HashSet<HashSet<Keyboard.Key>>();
        [TomlProperty("output")] public HID_USAGES OutputAxis { get; set;}
        [TomlProperty("regions")] public Dictionary<string, RegionConfig> Regions { get; set;}
    }

    public class RegionConfig {
        [TomlProperty("positive")] public double Positive { get; set; }
        [TomlProperty("neutral")] public double Neutral { get; set; }
        [TomlProperty("negative")] public double Negative { get; set; }
    }

    public class KeyConfig {
        [TomlProperty("ignore")] public HashSet<HashSet<Keyboard.Key>> IgnoreKC { get; set;} = new HashSet<HashSet<Keyboard.Key>>();
        [TomlProperty("recenter")] public HashSet<HashSet<Keyboard.Key>> RecenterKC { get; set;} = new HashSet<HashSet<Keyboard.Key>>();
        [TomlProperty("toggle")] public HashSet<HashSet<Keyboard.Key>> ToggleKC { get; set;} = new HashSet<HashSet<Keyboard.Key>>();
    }

    public class VisualizationConfig {
        [TomlProperty("window")] public LayoutWithBGColor Window { get; set;}
        [TomlProperty("sections")] public Dictionary<string, VisSection> Sections { get; set; }
    }

    public class VisSection {
        [TomlProperty("axis")] public VisSectionAxis Axis { get; set;}
        [TomlProperty("layout")] public LayoutWithBGColor Layout { get; set;}
        [TomlProperty("bars")] public VisSectionBars Bars { get; set;}
        [TomlProperty("ball")] public VisSectionBall Ball { get; set;}
    }

    public class LayoutWithBGColor {
        [TomlProperty("size")] public Size Size { get; set; }
        [TomlProperty("position")] public Position Position  { get; set; }
        [TomlProperty("anchor")] public Position Anchor  { get; set; } = new Position {X = 0.0, Y = 0.0};
        [TomlProperty("background_color")] public Color? BackgroundColor { get; set; } = null;
    }

    public class Size {
        [TomlProperty("width")] public double Width { get; set; }
        [TomlProperty("height")] public double Height { get; set; }
    }

    public class Position {
        [TomlProperty("x")] public double X { get; set; }
        [TomlProperty("y")] public double Y { get; set; }
    }

    public class VisSectionAxis {
        [TomlProperty("x")] public string? X { get; set; }
        [TomlProperty("y")] public string? Y { get; set; }
    }

    public class VisSectionBars {
        [TomlProperty("width")] public uint Width { get; set; }
        [TomlProperty("color")] public Color Color { get; set; }
    }

        public class VisSectionBall {
        // [TomlProperty("circular")] public bool Circular { get; set; } = false; 
        [TomlProperty("size")] public uint Width { get; set; } // Diameter/Sidelength
        [TomlProperty("color")] public Color Color { get; set; }
    }
}