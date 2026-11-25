using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace TaskbarMin;

enum Mode { Transparent = 0, Blur = 1, Acrylic = 2, Normal = 3 }

static class Native
{
    public enum Accent
    {
        Disabled = 0,
        EnableGradient = 1,
        EnableTransparentGradient = 2,
        EnableBlurBehind = 3,
        EnableAcrylicBlurBehind = 4
    }

    [StructLayout(LayoutKind.Sequential)]
    struct AccentPolicy
    {
        public Accent AccentState;
        public int AccentFlags;
        public uint GradientColor; // 0xAABBGGRR
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    const int WCA_ACCENT_POLICY = 19;

    public const uint WM_CLOSE = 0x0010;
    public const uint WM_APP_QUIT = 0x8001; // WM_APP + 1: orden interna de cierre
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;

    public delegate void WinEventDelegate(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

    [DllImport("user32.dll")]
    static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr FindWindow(string? cls, string? title);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? cls, string? title);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern uint RegisterWindowMessage(string msg);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, WinEventDelegate proc, uint pid, uint tid, uint flags);

    [DllImport("user32.dll")]
    public static extern bool UnhookWinEvent(IntPtr hook);

    /// <summary>Barra principal + una por cada monitor secundario.</summary>
    public static IEnumerable<IntPtr> GetTaskbars()
    {
        var main = FindWindow("Shell_TrayWnd", null);
        if (main != IntPtr.Zero) yield return main;

        var sec = IntPtr.Zero;
        while ((sec = FindWindowEx(IntPtr.Zero, sec, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
            yield return sec;
    }

    public static void Apply(IntPtr hwnd, Accent state, uint color)
    {
        var policy = new AccentPolicy { AccentState = state, AccentFlags = 2, GradientColor = color };
        int size = Marshal.SizeOf<AccentPolicy>();
        IntPtr ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(policy, ptr, false);
            var data = new WindowCompositionAttributeData { Attribute = WCA_ACCENT_POLICY, Data = ptr, SizeOfData = size };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally { Marshal.FreeHGlobal(ptr); }
    }
}

static class Settings
{
    const string Key = @"Software\TaskbarMin";
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunName = "TaskbarMin";

    public static (Mode mode, int opacity) Load()
    {
        using var k = Registry.CurrentUser.OpenSubKey(Key);
        var mode = k?.GetValue("Mode") is int m && Enum.IsDefined(typeof(Mode), m) ? (Mode)m : Mode.Transparent;
        var opacity = k?.GetValue("Opacity") is int o ? Math.Clamp(o, 0, 100) : 40;
        return (mode, opacity);
    }

    public static void Save(Mode mode, int opacity)
    {
        using var k = Registry.CurrentUser.CreateSubKey(Key);
        k.SetValue("Mode", (int)mode, RegistryValueKind.DWord);
        k.SetValue("Opacity", opacity, RegistryValueKind.DWord);
    }

    public static bool GetAutostart()
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey);
        return k?.GetValue(RunName) is not null;
    }

    public static void SetAutostart(bool on)
    {
        using var k = Registry.CurrentUser.CreateSubKey(RunKey);
        if (on) k.SetValue(RunName, $"\"{Environment.ProcessPath}\"");
        else k.DeleteValue(RunName, false);
    }
}

/// <summary>
/// Ventana oculta que recibe: "TaskbarCreated" (explorer.exe reiniciado),
/// WM_CLOSE y la orden interna de cierre (instalador / --quit).
/// </summary>
sealed class MsgWindow : NativeWindow
{
    readonly uint taskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");
    readonly Action onTaskbarCreated;
    readonly Action onQuit;

    public MsgWindow(Action onTaskbarCreated, Action onQuit)
    {
        this.onTaskbarCreated = onTaskbarCreated;
        this.onQuit = onQuit;
        CreateHandle(new CreateParams());
    }

    protected override void WndProc(ref Message m)
    {
        if ((uint)m.Msg == taskbarCreated) onTaskbarCreated();
        else if ((uint)m.Msg == Native.WM_APP_QUIT || (uint)m.Msg == Native.WM_CLOSE) { onQuit(); return; }
        base.WndProc(ref m);
    }
}

sealed class TrayApp : ApplicationContext
{
    readonly NotifyIcon tray;
    readonly System.Windows.Forms.Timer safetyTimer;
    readonly MsgWindow msgWindow;
    readonly Native.WinEventDelegate hookProc;
    readonly IntPtr hook;
    readonly Dictionary<Mode, ToolStripMenuItem> modeItems = new();
    readonly Dictionary<int, ToolStripMenuItem> opacityItems = new();
    readonly ToolStripMenuItem autostartItem;

    Mode mode;
    int opacity;
    bool quitting;

    static readonly (Mode mode, string label)[] Modes =
    {
        (Mode.Transparent, "Transparente"),
        (Mode.Blur, "Desenfoque"),
        (Mode.Acrylic, "Acrílico"),
        (Mode.Normal, "Normal (Windows)")
    };

    static readonly int[] Opacities = { 0, 20, 40, 60, 80 };

    public TrayApp(EventWaitHandle quitEvent)
    {
        (mode, opacity) = Settings.Load();

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "";
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem($"TaskbarMin {version}") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());

        foreach (var (m, label) in Modes)
        {
            var item = new ToolStripMenuItem(label);
            var captured = m;
            item.Click += (_, _) => SetMode(captured);
            modeItems[m] = item;
            menu.Items.Add(item);
        }

        menu.Items.Add(new ToolStripSeparator());

        var opacityMenu = new ToolStripMenuItem("Opacidad (desenfoque/acrílico)");
        foreach (var o in Opacities)
        {
            var item = new ToolStripMenuItem($"{o}%");
            var captured = o;
            item.Click += (_, _) => SetOpacity(captured);
            opacityItems[o] = item;
            opacityMenu.DropDownItems.Add(item);
        }
        menu.Items.Add(opacityMenu);

        autostartItem = new ToolStripMenuItem("Iniciar con Windows") { Checked = Settings.GetAutostart() };
        autostartItem.Click += (_, _) =>
        {
            autostartItem.Checked = !autostartItem.Checked;
            Settings.SetAutostart(autostartItem.Checked);
        };
        menu.Items.Add(autostartItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) => Quit());

        tray = new NotifyIcon
        {
            Icon = BuildIcon(),
            Text = "TaskbarMin",
            ContextMenuStrip = menu,
            Visible = true
        };

        UpdateChecks();
        ApplyAll();

        // 1) Reaplicar al cambiar la ventana en primer plano (basado en eventos, coste ~0).
        hookProc = (_, _, _, _, _, _, _) => ApplyAll();
        hook = Native.SetWinEventHook(Native.EVENT_SYSTEM_FOREGROUND, Native.EVENT_SYSTEM_FOREGROUND,
                                      IntPtr.Zero, hookProc, 0, 0, 0);

        // 2) Reaplicar si explorer.exe se reinicia + cierre ordenado desde fuera.
        msgWindow = new MsgWindow(ApplyAll, Quit);

        // 3) "TaskbarMin.exe --quit" (instalador/desinstalador) activa este evento.
        ThreadPool.RegisterWaitForSingleObject(
            quitEvent,
            (_, _) => Native.PostMessage(msgWindow.Handle, Native.WM_APP_QUIT, IntPtr.Zero, IntPtr.Zero),
            null, Timeout.Infinite, true);

        // 4) Red de seguridad: Windows a veces restablece el estilo (menú inicio, etc.).
        safetyTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        safetyTimer.Tick += (_, _) => ApplyAll();
        safetyTimer.Start();
    }

    void SetMode(Mode m)
    {
        mode = m;
        Settings.Save(mode, opacity);
        UpdateChecks();
        ApplyAll();
    }

    void SetOpacity(int o)
    {
        opacity = o;
        Settings.Save(mode, opacity);
        UpdateChecks();
        ApplyAll();
    }

    void UpdateChecks()
    {
        foreach (var (m, item) in modeItems) item.Checked = m == mode;
        foreach (var (o, item) in opacityItems) item.Checked = o == opacity;
    }

    uint Tint() => (uint)(opacity * 255 / 100) << 24; // negro con alfa

    void ApplyAll()
    {
        if (quitting) return;

        var (state, color) = mode switch
        {
            Mode.Transparent => (Native.Accent.EnableTransparentGradient, 0u),
            Mode.Blur => (Native.Accent.EnableBlurBehind, Tint()),
            Mode.Acrylic => (Native.Accent.EnableAcrylicBlurBehind, Tint()),
            _ => (Native.Accent.Disabled, 0u)
        };

        foreach (var hwnd in Native.GetTaskbars())
            Native.Apply(hwnd, state, color);
    }

    void Quit()
    {
        if (quitting) return;
        quitting = true;

        safetyTimer.Stop();
        Native.UnhookWinEvent(hook);

        // Devolver la barra a su estado original.
        foreach (var hwnd in Native.GetTaskbars())
            Native.Apply(hwnd, Native.Accent.Disabled, 0);

        tray.Visible = false;
        tray.Dispose();
        ExitThread();
    }

    static Icon BuildIcon()
    {
        using var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            using var b = new SolidBrush(Color.FromArgb(220, 255, 255, 255));
            g.FillRectangle(b, 1, 11, 14, 4); // "barra de tareas"
            using var p = new Pen(Color.FromArgb(220, 255, 255, 255));
            g.DrawRectangle(p, 1, 1, 13, 13);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }
}

static class Program
{
    const string MutexName = @"Local\TaskbarMin.SingleInstance";
    const string QuitEventName = @"Local\TaskbarMin.Quit";

    [STAThread]
    static void Main(string[] args)
    {
        bool quitRequested = args.Contains("--quit", StringComparer.OrdinalIgnoreCase);

        using var mutex = new Mutex(true, MutexName, out bool isNew);

        if (quitRequested)
        {
            // Pide cerrar a la instancia en ejecución (si la hay) y sale.
            if (!isNew && EventWaitHandle.TryOpenExisting(QuitEventName, out var existing))
            {
                existing.Set();
                existing.Dispose();
            }
            return;
        }

        if (!isNew) return; // ya hay una instancia en ejecución

        using var quitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, QuitEventName);

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp(quitEvent));
    }
}
