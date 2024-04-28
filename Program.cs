using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using SFML.Graphics;
using SFML.Window;
using Tomlet;
using Tomlet.Exceptions;
using Tomlet.Models;
using vJoyInterfaceWrap;

/*
Credits: 
DownloadPizza for typing
Renblo for imagining the entire program and channeling the code into DownloadPizza's hands
*/

namespace ChronoCurves
{
    public enum Direction
    {
        Positive,
        Negative,
        Neutral
    }

    internal class Program
    {
        private static void Main()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            
            new ChronoCurves()
                .MainLoop();
        }
    }

    class ChronoCurves
    {
        const uint JOYSTICK_ID = 1;
        private bool active = true;
        private bool toggleHeld = false;
        private OverlayWindow _window;
        private vJoy joystick;
        private Dictionary<Keyboard.Key, bool> _keysPressed;
        private Config _config;
        private Dictionary<string, KBAxis> _axisDict;
        private Dictionary<string, OutputAxisInfo> _axisOutputs;

        public ChronoCurves()
        {
            SetupTomletMappers();
            joystick = SetupVJoy();

            var configText = File.ReadAllText("config.toml");
            _config = TomletMain.To<Config>(configText);

            _axisDict = _config.Axis.ToDictionary(
                kv => kv.Key,
                kv =>
                {
                    var regions = kv.Value.Regions.Select(kv =>
                    {
                        var range = OpenClosedRange.Parse(kv.Key);
                        return new SnapRegion(range, kv.Value.Positive, kv.Value.Neutral, kv.Value.Negative);
                    }).ToArray();

                    Array.Sort(regions, (a, b) => a.OCRange.CompareTo(b.OCRange));

                    return new KBAxis(kv.Value.NegativeKC, kv.Value.PositiveKC, regions);
                }
            );

            _axisOutputs = _config.Axis.ToDictionary(kv => kv.Key, kv =>
            {
                return new OutputAxisInfo
                {
                    Axis = kv.Value.OutputAxis,
                    Center = 16384,
                    Radius = 16384
                };
            });

            _window = new OverlayWindow(_config, _axisDict);

            joystick.ResetVJD(JOYSTICK_ID);

            var keyCombos = _config.Keys.IgnoreKC
                .Concat(_config.Keys.RecenterKC)
                .Concat(_config.Keys.ToggleKC)
                .Concat(_config.Axis.SelectMany(x => x.Value.NegativeKC))
                .Concat(_config.Axis.SelectMany(x => x.Value.PositiveKC))
                .ToHashSet();

            _keysPressed = keyCombos.SelectMany(x => x).Distinct().ToDictionary(k => k, k => false);
        }

        public void MainLoop() {
            Stopwatch sw = new();
            sw.Start();

            while (_window.IsOpen)
            {
                CollectKeys();

                var elapsed = sw.ElapsedTicks * 1e6 / Stopwatch.Frequency;
                sw.Restart();

                var recenterPressed = IsAnyKeyComboPressed(_config.Keys.RecenterKC);
                var ignorePressed = IsAnyKeyComboPressed(_config.Keys.IgnoreKC);
                var togglePressed = IsAnyKeyComboPressed(_config.Keys.ToggleKC);

                if(togglePressed) {
                    if(!toggleHeld) 
                        active = !active;
                    toggleHeld = true;
                } else {
                    toggleHeld = false;
                }

                foreach (var (name, axis) in _axisDict)
                {
                    if (recenterPressed)
                    {
                        axis.Value = 0.0;
                    }

                    var direction = Direction.Neutral;

                    if(!ignorePressed && !recenterPressed && active) 
                    {
                        var negativePressed = IsAnyKeyComboPressed(axis.Negative);
                        var positivePressed = IsAnyKeyComboPressed(axis.Positive);

                        if (negativePressed) direction = Direction.Negative;
                        if (positivePressed) direction = Direction.Positive;
                        if (negativePressed && positivePressed) direction = Direction.Neutral;
                    }

                    axis.Apply(direction, elapsed);

                    var outputInfo = _axisOutputs[name];
                    joystick.SetAxis((int)(outputInfo.Center + outputInfo.Radius * axis.Value), JOYSTICK_ID, outputInfo.Axis);
                    // if (name == "pitch") Console.WriteLine((int)(outputInfo.Center + outputInfo.Radius * axis.Value));
                }

                _window.Run();

                Thread.Sleep(15); // refresh 66 times per second
            }
        }

        private static vJoy SetupVJoy()
        {
            var joystick = new vJoy();

            if (!joystick.vJoyEnabled())
            {
                throw new Exception("vJoy driver not enabled: Failed Getting vJoy attributes.\n");
            }
            else
            {
                Console.WriteLine("Vendor: {0}\nProduct :{1}\nVersion Number:{2}\n",
                joystick.GetvJoyManufacturerString(),
                joystick.GetvJoyProductString(),
                joystick.GetvJoySerialNumberString());
            }

            uint DllVer = 0, DrvVer = 0;
            bool match = joystick.DriverMatch(ref DllVer, ref DrvVer);
            if (match)
                Console.WriteLine("Version of Driver Matches DLL Version ({0:X})\n", DllVer);
            else
                Console.WriteLine("Version of Driver ({0:X}) does NOT match DLL Version ({1:X}) (this may or may not become a problem)\n", DrvVer, DllVer);

            VjdStat status = joystick.GetVJDStatus(JOYSTICK_ID);

            // Acquire the target
            if ((status == VjdStat.VJD_STAT_OWN) ||
                    ((status == VjdStat.VJD_STAT_FREE) && (!joystick.AcquireVJD(JOYSTICK_ID))))
                throw new Exception($"Failed to acquire vJoy device number {JOYSTICK_ID}.");
            else
                Console.WriteLine($"Acquired: vJoy device number {JOYSTICK_ID}.");

            return joystick;
        }

        private static void SetupTomletMappers()
        {
            TomletMain.RegisterMapper<HashSet<HashSet<Keyboard.Key>>>(
                keyCombos => null,
                tomlValue =>
                {
                    var context = typeof(HashSet<HashSet<Keyboard.Key>>);
                    var tomlArray = ConvertOrThrow<TomlArray>(tomlValue, context);

                    return tomlArray.Select(keys =>
                    {
                        var tomlString = ConvertOrThrow<TomlString>(keys, context);
                        return tomlString.Value.Split("+").Select(key => Enum.Parse<Keyboard.Key>(key)).ToHashSet();
                    }).ToHashSet();
                }
            );

            TomletMain.RegisterMapper<Color>(
                color => null,
                tomlValue =>
                {
                    var tomlTable = ConvertOrThrow<TomlTable>(tomlValue, typeof(Color));
                    var r = (byte)tomlTable.GetLong("r");
                    var g = (byte)tomlTable.GetLong("g");
                    var b = (byte)tomlTable.GetLong("b");
                    var a = (byte)tomlTable.GetLong("alpha");

                    return new Color(r, g, b, a);
                }
            );

            TomletMain.RegisterMapper<HID_USAGES>(
                hidUsage => new TomlString(Enum.GetName(hidUsage).Replace("HID_USAGE_", "")),
                tomlValue =>
                {
                    var tomlString = ConvertOrThrow<TomlString>(tomlValue, typeof(HID_USAGES));

                    return Enum.Parse<HID_USAGES>($"HID_USAGE_{tomlString}");
                }
            );
        }

        private static T ConvertOrThrow<T>(TomlValue tomlValue, Type context)
        {
            if (tomlValue is not T t)
                throw new TomlTypeMismatchException(typeof(T), tomlValue.GetType(), context);
            return t;
        }

        private void CollectKeys()
        {
            foreach (var key in _keysPressed.Keys)
            {
                _keysPressed[key] = Keyboard.IsKeyPressed(key);
            }
        }

        private bool IsKeyComboPressed(HashSet<Keyboard.Key> keyCombo) => keyCombo.All(x => _keysPressed[x]);
        private bool IsAnyKeyComboPressed(HashSet<HashSet<Keyboard.Key>> keyCombos) => keyCombos.Any(IsKeyComboPressed);
    }

    struct OutputAxisInfo
    {
        public HID_USAGES Axis;
        public long Center;
        public long Radius;
    }
}