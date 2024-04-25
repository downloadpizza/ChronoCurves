using System.Runtime.InteropServices;
using SFML.Graphics;
using SFML.System;
using SFML.Window;

namespace ChronoCurves
{
    partial class WindowUtilities {
        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

		[DllImport ("dwmapi.dll")]
		public static extern IntPtr DwmEnableBlurBehindWindow (IntPtr hWnd, ref DWM_BLURBEHIND pBlurBehind);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    }

    enum DWM_BB : uint
    {
        Enable = 0x0001,
        BlurRegion = 0x0002,
        // rest omitted
    }

    [StructLayout (LayoutKind.Sequential)]
    struct DWM_BLURBEHIND
    {
        public DWM_BB dwFlags;
        public bool fEnable;
        public IntPtr hRgnBlur;
        public bool fTransitionOnMaximized;
    }

    enum WindowLongFlags : int
    {
        EXSTYLE = -20,
        // rest ommited
    }

    enum EXWindowStyleFlags : int {
        LAYERED = 0x00080000,
        TRANSPARENT = 0x00000020,
        // rest ommited
    }

    enum ZOrder : int {
        TOPMOST = -1,
        // rest omitted
    }



    class OverlayWindow
    {
        public bool IsOpen { get => _window.IsOpen; }
        private readonly Config _config;
        private readonly Dictionary<string, KBAxis> _axis;
        
        private readonly RenderWindow _window;
        private readonly List<Drawable> _staticDrawables;

        public OverlayWindow(Config config, Dictionary<string, KBAxis> axis)
        {
            _config = config;
            _axis = axis;

            
            var windowVis = _config.Visualization.Window;

            _window = new RenderWindow(
                new VideoMode((uint)windowVis.Size.Width, (uint)windowVis.Size.Height),
                "AxisOverlay", 
                Styles.None)
            {
                Position = new Vector2i(
                    (int)(VideoMode.DesktopMode.Width * windowVis.Position.X - windowVis.Size.Width * windowVis.Anchor.X), 
                    (int)(VideoMode.DesktopMode.Height * windowVis.Position.Y - windowVis.Size.Height * windowVis.Anchor.Y)
                )
            };
            WindowUtilities.SetWindowPos(_window.SystemHandle, (nint)ZOrder.TOPMOST, 0, 0, 0, 0, 0x0002 | 0x0001);
            WindowUtilities.SetWindowLong(_window.SystemHandle, (int)WindowLongFlags.EXSTYLE, (int)(EXWindowStyleFlags.LAYERED | EXWindowStyleFlags.TRANSPARENT));
            var blurBehindParameters = new DWM_BLURBEHIND
            {
                dwFlags = DWM_BB.Enable,
                fEnable = true,
            };

            WindowUtilities.DwmEnableBlurBehindWindow(_window.SystemHandle, ref blurBehindParameters);

            _window.Closed += OnClose;

            var sectionBackgrounds = new List<Drawable>();
            var bars = new List<Drawable>();

            foreach(var (_, section) in _config.Visualization.Sections) {
                var sectionVis = section.Layout;

                var hasXAxis = section.Axis.X is not null;
                var hasYAxis = section.Axis.Y is not null;

                var rectWidth = sectionVis.Size.Width * windowVis.Size.Width;
                var rectHeight = sectionVis.Size.Height * windowVis.Size.Height;

                var rectX = sectionVis.Position.X * windowVis.Size.Width - sectionVis.Anchor.X * rectWidth;
                var rectY = sectionVis.Position.Y * windowVis.Size.Height - sectionVis.Anchor.Y * rectHeight;
                
                if(sectionVis.BackgroundColor is not null) {                    
                    sectionBackgrounds.Add(new RectangleShape(new Vector2f(
                        (float) rectWidth,
                        (float) rectHeight
                    )) {
                        FillColor = sectionVis.BackgroundColor.Value,
                        Position = new Vector2f(
                            (float) rectX,
                            (float) rectY
                        )
                    });
                }

                if(hasXAxis) {
                    var barLeft = rectX;

                    var barTop = rectY + rectHeight/2 - section.Bars.Width/2;
                    
                    bars.Add(new RectangleShape(new Vector2f((float)rectWidth, section.Bars.Width)) {
                        FillColor = section.Bars.Color,
                        Position = new Vector2f((float)barLeft, (float)barTop)
                    });
                }

                if(hasYAxis) {
                    var barLeft = rectX + rectWidth/2 - section.Bars.Width/2;
                    var barTop = rectY;
                    
                    bars.Add(new RectangleShape(new Vector2f(section.Bars.Width, (float)rectHeight)) {
                        FillColor = section.Bars.Color,
                        Position = new Vector2f((float)barLeft, (float)barTop)
                    });
                }
            }

            _staticDrawables = sectionBackgrounds.Concat(bars).ToList();
        }

        private void OnClose(object sender, EventArgs e)
        {
            _window.Close();
        }

        internal void Run()
        {
            var windowVis = _config.Visualization.Window;

            var bgColor = windowVis.BackgroundColor ?? Color.Black;

            _window.Clear(bgColor);
            _window.DispatchEvents();

            
            // draw static elements
            foreach(var staticDrawable in _staticDrawables) {
                _window.Draw(staticDrawable);
            }

            foreach(var (name, section) in _config.Visualization.Sections) {
                var sectionVis = section.Layout;

                var hasXAxis = section.Axis.X is not null;
                var hasYAxis = section.Axis.Y is not null;

                var rectWidth = sectionVis.Size.Width * windowVis.Size.Width;
                var rectHeight = sectionVis.Size.Height * windowVis.Size.Height;

                var rectXCenter = sectionVis.Position.X * windowVis.Size.Width - sectionVis.Anchor.X * rectWidth + rectWidth/2;
                var rectYCenter = sectionVis.Position.Y * windowVis.Size.Height - sectionVis.Anchor.Y * rectHeight + rectHeight/2;

                var ballX = hasXAxis ? _axis[section.Axis.X].Value * (rectWidth/2) : 0;   
                var ballY = hasYAxis ? _axis[section.Axis.Y].Value * (rectHeight/2) * -1 : 0;   
                
                _window.Draw(new RectangleShape(new Vector2f(section.Ball.Width, section.Ball.Width)) {
                    FillColor = section.Ball.Color,
                    Position = new Vector2f((float)(rectXCenter + ballX - section.Ball.Width/2), (float)(rectYCenter + ballY - section.Ball.Width/2))
                });
            }

            _window.Display();
        }
    }
}