using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Teclado10Key
{
    // ================= Motor 천지인 =================
    sealed class State
    {
        public string Text = "", L = "", V = "", T = "";
        public State Clone() { State s = new State(); s.Text = Text; s.L = L; s.V = V; s.T = T; return s; }
    }

    sealed class Engine
    {
        public State S = new State();
        public List<State> H = new List<State>();
        public List<State> R = new List<State>();
        public bool Digits;
        public string MarkCode; public int MarkIdx; public int MarkTs;
        bool cycOn; string cycCode; int cycI; int cycTs;
        const int Timeout = 1000;

        static readonly Dictionary<string, string> Normal = new Dictionary<string, string> {
            {"Numpad0","0"},{"Numpad1","1"},{"Numpad2","2"},{"Numpad3","3"},{"Numpad4","4"},
            {"Numpad5","5"},{"Numpad6","6"},{"Numpad7","7"},{"Numpad8","8"},{"Numpad9","9"},
            {"NumpadAdd","+"},{"NumpadSubtract","-"},{"NumpadMultiply","*"},{"NumpadDivide","/"},{"NumpadDecimal","."}
        };
        static readonly Dictionary<string, string> Stroke = new Dictionary<string, string> {
            {"Numpad7","1"},{"Numpad8","2"},{"Numpad9","3"}
        };

        public static string Comp(State s)
        {
            if (s.L.Length == 0 && s.V.Length == 0) return "";
            if (s.V.Length == 0) return s.L;
            string vv = Data.VT[s.V];
            int vi = Array.IndexOf(Data.Vowel, vv);
            if (vi < 0) return s.L + vv;
            if (s.L.Length == 0) return vv;
            string tj = s.T.Length == 2 ? Data.CF[s.T] : s.T;
            int ti = Array.IndexOf(Data.Tail, tj);
            int li = Array.IndexOf(Data.Lead, s.L);
            if (li < 0 || ti < 0) return s.L + vv;
            return ((char)(0xAC00 + (li * 21 + vi) * 28 + ti)).ToString();
        }
        static bool IsV(string v)
        {
            string vv;
            return Data.VT.TryGetValue(v, out vv) && Array.IndexOf(Data.Vowel, vv) >= 0;
        }
        static void Commit(State s) { s.Text += Comp(s); s.L = ""; s.V = ""; s.T = ""; }
        static void Cons(State s, string c)
        {
            if (s.L.Length == 0 && s.V.Length == 0) { s.L = c; return; }
            if (s.V.Length > 0 && !IsV(s.V)) { Commit(s); s.L = c; return; }
            if (s.V.Length > 0 && s.L.Length > 0)
            {
                if (s.T.Length == 0) { if (Array.IndexOf(Data.Tail, c) > 0) { s.T = c; return; } }
                else if (s.T.Length == 1 && Data.CF.ContainsKey(s.T + c)) { s.T += c; return; }
            }
            Commit(s); s.L = c;
        }
        static void Vow(State s, string k)
        {
            if (s.T.Length > 0)
            {
                string m;
                if (s.T.Length == 2) { m = s.T.Substring(1, 1); s.T = s.T.Substring(0, 1); }
                else { m = s.T; s.T = ""; }
                Commit(s); s.L = m; s.V = k; return;
            }
            string n = s.V + k;
            if (Data.VT.ContainsKey(n)) { s.V = n; return; }
            Commit(s); s.V = k;
        }
        static void Put(State s, string ch) { Commit(s); s.Text += ch; }

        public string Full() { return S.Text + Comp(S); }
        void SaveHistory() { H.Add(S.Clone()); if (H.Count > 3000) H.RemoveAt(0); R.Clear(); }
        public void CancelCycle() { cycOn = false; MarkCode = null; }
        public void ToggleDigits() { Digits = !Digits; CancelCycle(); }
        public void Undo() { CancelCycle(); if (H.Count > 0) { R.Add(S.Clone()); S = H[H.Count - 1]; H.RemoveAt(H.Count - 1); } }
        public void Redo() { CancelCycle(); if (R.Count > 0) { H.Add(S.Clone()); S = R[R.Count - 1]; R.RemoveAt(R.Count - 1); } }
        public void Clear() { SaveHistory(); S = new State(); CancelCycle(); }

        // devolve true se foi uma repetição (ciclo) da mesma tecla de consoante
        public bool Press(string code)
        {
            if (Digits && Normal.ContainsKey(code)) { CancelCycle(); SaveHistory(); Put(S, Normal[code]); return false; }
            if (code == "NumpadSubtract" || code == "Backspace")
            {
                CancelCycle(); SaveHistory();
                if (S.T.Length > 0) S.T = S.T.Substring(0, S.T.Length - 1);
                else if (S.V.Length > 0) S.V = S.V.Substring(0, S.V.Length - 1);
                else if (S.L.Length > 0) S.L = "";
                else if (S.Text.Length > 0) S.Text = S.Text.Substring(0, S.Text.Length - 1);
                return false;
            }
            int now = Environment.TickCount;
            string[] set;
            if (Data.CY.TryGetValue(code, out set))
            {
                bool cycled = false;
                if (cycOn && cycCode == code && unchecked(now - cycTs) < Timeout && H.Count > 0)
                {
                    cycI = (cycI + 1) % set.Length; cycTs = now; S = H[H.Count - 1].Clone(); cycled = true;
                }
                else { SaveHistory(); cycOn = true; cycCode = code; cycI = 0; cycTs = now; }
                string ch = set[cycI];
                if (code == "NumpadMultiply" || code == "NumpadDivide" || code == "NumpadDecimal") Put(S, ch); else Cons(S, ch);
                MarkCode = code; MarkIdx = cycI; MarkTs = now;
                return cycled;
            }
            CancelCycle(); SaveHistory();
            string st;
            if (Stroke.TryGetValue(code, out st)) Vow(S, st);
            else if (code == "NumpadAdd" || code == "Space") Put(S, " ");
            else if (code == "NumpadEnter" || code == "Enter") Put(S, "\n");
            return false;
        }
    }

    // ================= Teclas físicas (por scancode, independente do NumLock) =================
    static class KeyMap
    {
        public static string Map(int scan, bool ext)
        {
            if (!ext)
            {
                switch (scan)
                {
                    case 0x52: return "Numpad0";
                    case 0x4F: return "Numpad1";
                    case 0x50: return "Numpad2";
                    case 0x51: return "Numpad3";
                    case 0x4B: return "Numpad4";
                    case 0x4C: return "Numpad5";
                    case 0x4D: return "Numpad6";
                    case 0x47: return "Numpad7";
                    case 0x48: return "Numpad8";
                    case 0x49: return "Numpad9";
                    case 0x53: return "NumpadDecimal";
                    case 0x4E: return "NumpadAdd";
                    case 0x4A: return "NumpadSubtract";
                    case 0x37: return "NumpadMultiply";
                }
            }
            else
            {
                switch (scan)
                {
                    case 0x35: return "NumpadDivide";
                    case 0x1C: return "NumpadEnter";
                }
            }
            return null;
        }
    }

    // ================= Win32 =================
    [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] struct HARDWAREINPUT { public uint uMsg; public ushort wParamL; public ushort wParamH; }
    [StructLayout(LayoutKind.Explicit)]
    struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public InputUnion U; }
    [StructLayout(LayoutKind.Sequential)] struct KBDLL { public uint vkCode; public uint scanCode; public uint flags; public uint time; public IntPtr dwExtraInfo; }

    static class Native
    {
        public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint n, INPUT[] inputs, int size);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);
        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
    }

    static class Sender
    {
        static INPUT Vk(ushort vk, bool up)
        {
            INPUT i = new INPUT(); i.type = 1; i.U.ki.wVk = vk; i.U.ki.dwFlags = up ? 2u : 0u; return i;
        }
        static INPUT Uni(char c, bool up)
        {
            INPUT i = new INPUT(); i.type = 1; i.U.ki.wScan = c; i.U.ki.dwFlags = 4u | (up ? 2u : 0u); return i;
        }
        static void Send(List<INPUT> l)
        {
            if (l.Count == 0) return;
            Native.SendInput((uint)l.Count, l.ToArray(), Marshal.SizeOf(typeof(INPUT)));
        }
        public static void Key(ushort vk)
        {
            List<INPUT> l = new List<INPUT>(); l.Add(Vk(vk, false)); l.Add(Vk(vk, true)); Send(l);
        }
        public static void Backspaces(int n)
        {
            List<INPUT> l = new List<INPUT>();
            for (int i = 0; i < n; i++) { l.Add(Vk(0x08, false)); l.Add(Vk(0x08, true)); }
            Send(l);
        }
        public static void Text(string s)
        {
            List<INPUT> l = new List<INPUT>();
            foreach (char c in s)
            {
                if (c == '\n') { l.Add(Vk(0x0D, false)); l.Add(Vk(0x0D, true)); }
                else { l.Add(Uni(c, false)); l.Add(Uni(c, true)); }
            }
            Send(l);
        }
    }

    // ================= Utilitários gráficos / config =================
    static class Ui
    {
        public static float Dpi = 96f;
        public static int D(int v) { return (int)Math.Round(v * Dpi / 96.0); }
        static string ko;
        public static string KoFont
        {
            get
            {
                if (ko == null)
                {
                    ko = "Segoe UI";
                    try
                    {
                        using (InstalledFontCollection ifc = new InstalledFontCollection())
                        {
                            foreach (FontFamily f in ifc.Families)
                                if (string.Equals(f.Name, "Malgun Gothic", StringComparison.OrdinalIgnoreCase)) ko = f.Name;
                        }
                    }
                    catch (Exception) { }
                }
                return ko;
            }
        }
        public static GraphicsPath Round(Rectangle r, int rad)
        {
            int d = Math.Max(2, rad * 2);
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
        public static Color Mix(Color a, Color b, double t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }
        public static Icon MakeIcon(Color back, Color fore)
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    using (GraphicsPath p = Round(new Rectangle(0, 0, 31, 31), 7))
                    using (SolidBrush b = new SolidBrush(back)) g.FillPath(b, p);
                    using (Font f = new Font(KoFont, 19f, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (StringFormat sf = new StringFormat())
                    using (SolidBrush fb = new SolidBrush(fore))
                    {
                        sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
                        g.DrawString("\uD55C", f, fb, new RectangleF(0, 1, 32, 32), sf);
                    }
                }
                IntPtr h = bmp.GetHicon();
                Icon ic = (Icon)Icon.FromHandle(h).Clone();
                Native.DestroyIcon(h);
                return ic;
            }
        }
    }

    sealed class Theme
    {
        public Color Bg, Screen, Ink, Dim, Line, Key, Edge, VKey, VInk, UKey, Acc, AccInk;
        public static Theme Light()
        {
            Theme t = new Theme();
            t.Bg = Color.FromArgb(0xEA, 0xF0, 0xF8); t.Screen = Color.White; t.Ink = Color.FromArgb(0x1B, 0x1B, 0x1F);
            t.Dim = Color.FromArgb(0x5D, 0x5D, 0x66); t.Line = Color.FromArgb(0xC9, 0xD0, 0xDA);
            t.Key = Color.White; t.Edge = Color.FromArgb(0xB4, 0xBC, 0xC8);
            t.VKey = Color.FromArgb(0xDD, 0xE4, 0xEC); t.VInk = Color.FromArgb(0x3A, 0x4B, 0x5C);
            t.UKey = Color.FromArgb(0xF2, 0xF5, 0xF9); t.Acc = Color.FromArgb(0x0F, 0x6C, 0xBD); t.AccInk = Color.White;
            return t;
        }
        public static Theme Dark()
        {
            Theme t = new Theme();
            t.Bg = Color.FromArgb(0x12, 0x15, 0x1E); t.Screen = Color.FromArgb(0x0A, 0x0C, 0x12); t.Ink = Color.FromArgb(0xF3, 0xF4, 0xF7);
            t.Dim = Color.FromArgb(0xA3, 0xA7, 0xB3); t.Line = Color.FromArgb(0x3A, 0x3F, 0x4D);
            t.Key = Color.FromArgb(0x2B, 0x2F, 0x3B); t.Edge = Color.FromArgb(0x05, 0x06, 0x0A);
            t.VKey = Color.FromArgb(0x33, 0x3B, 0x4A); t.VInk = Color.FromArgb(0xCB, 0xD5, 0xE1);
            t.UKey = Color.FromArgb(0x20, 0x24, 0x2E); t.Acc = Color.FromArgb(0x47, 0x9E, 0xF5); t.AccInk = Color.FromArgb(0x06, 0x12, 0x1F);
            return t;
        }
    }

    static class Settings
    {
        public static string Lang, Theme; public static bool Top;
        static string FilePath { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Teclado10Key.ini"); } }
        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    int i = line.IndexOf('=');
                    if (i <= 0) continue;
                    string k = line.Substring(0, i).Trim(), v = line.Substring(i + 1).Trim();
                    if (k == "lang") Lang = v; else if (k == "theme") Theme = v; else if (k == "top") Top = (v == "1");
                }
            }
            catch (Exception) { }
        }
        public static void Save()
        {
            try { File.WriteAllText(FilePath, "lang=" + Lang + "\r\ntheme=" + Theme + "\r\ntop=" + (Top ? "1" : "0") + "\r\n", Encoding.UTF8); }
            catch (Exception) { }
        }
    }

    static class AutoStart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValName = "Teclado10Key";
        public static bool Get()
        {
            try { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue(ValName) != null; }
            catch (Exception) { return false; }
        }
        public static void Set(bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    if (on) k.SetValue(ValName, "\"" + Application.ExecutablePath + "\" /min"); else k.DeleteValue(ValName, false);
                }
            }
            catch (Exception) { }
        }
    }

    // ================= Tecla virtual =================
    sealed class KeyBtn : Control
    {
        public string Code, Num, Tip; public string[] Glyphs; public int Kind; // 0 consoante, 1 vogal, 2 utilitária
        public bool Down, Hover, Toggled, Flash; public int FlashTs; public int Cur = -1;
        public Theme Th;
        public Action<KeyBtn> Clicked;
        Font fBig, fSmall, fNum;
        const TextFormatFlags TF = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.Left | TextFormatFlags.Top;

        public KeyBtn(string code, string num, string[] glyphs, int kind)
        {
            Code = code; Num = num; Glyphs = glyphs; Kind = kind;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false; Cursor = Cursors.Hand;
            string fam = kind == 2 ? "Segoe UI Symbol" : Ui.KoFont;
            fBig = new Font(fam, kind == 1 ? 17f : (kind == 2 ? 12f : 15f));
            fSmall = new Font(fam, 9.5f);
            fNum = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { fBig.Dispose(); fSmall.Dispose(); fNum.Dispose(); }
            base.Dispose(disposing);
        }
        protected override void OnMouseEnter(EventArgs e) { Hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { Hover = false; Down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { Down = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            bool fire = Down && ClientRectangle.Contains(e.Location);
            Down = false; Invalidate();
            if (fire && Clicked != null) Clicked(this);
            base.OnMouseUp(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Th.Bg);
            int edge = Ui.D(2), rad = Ui.D(7);
            bool pressed = Down || Flash;
            int off = pressed ? edge : 0;
            Color fill = Toggled ? Th.Acc : (Kind == 2 ? Th.UKey : (Kind == 1 ? Th.VKey : Th.Key));
            if (Hover && !pressed) fill = Ui.Mix(fill, Th.Ink, 0.06);
            Color ink = Toggled ? Th.AccInk : (Kind == 1 ? Th.VInk : Th.Ink);
            if (!pressed)
            {
                using (GraphicsPath pe = Ui.Round(new Rectangle(0, edge, Width - 1, Height - 1 - edge), rad))
                using (SolidBrush b = new SolidBrush(Th.Edge)) g.FillPath(b, pe);
            }
            using (GraphicsPath pf = Ui.Round(new Rectangle(0, off, Width - 1, Height - 1 - edge), rad))
            {
                using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, pf);
                using (Pen pen = new Pen(Th.Line)) g.DrawPath(pen, pf);
            }
            TextRenderer.DrawText(g, Num, fNum, new Point(Ui.D(9), off + Ui.D(5)), Toggled ? Th.AccInk : Th.Dim, TF);

            int n = Glyphs.Length, padX = Ui.D(4), gap = Ui.D(2), total = 0, maxH = 0;
            Size[] sz = new Size[n];
            for (int i = 0; i < n; i++)
            {
                sz[i] = TextRenderer.MeasureText(g, Glyphs[i], i == 0 ? fBig : fSmall, new Size(int.MaxValue, int.MaxValue), TF);
                total += sz[i].Width + padX * 2;
                if (i > 0) total += gap;
                if (sz[i].Height > maxH) maxH = sz[i].Height;
            }
            int x = (Width - total) / 2;
            int y = off + (Height - edge - maxH) / 2 + Ui.D(3);
            for (int i = 0; i < n; i++)
            {
                int cw = sz[i].Width + padX * 2;
                bool isCur = (i == Cur);
                if (isCur)
                {
                    using (GraphicsPath gp = Ui.Round(new Rectangle(x, y - Ui.D(1), cw, sz[i].Height + Ui.D(2)), Ui.D(4)))
                    using (SolidBrush b = new SolidBrush(Th.Acc)) g.FillPath(b, gp);
                }
                Color c = isCur ? Th.AccInk : (i == 0 ? ink : Ui.Mix(ink, fill, 0.5));
                TextRenderer.DrawText(g, Glyphs[i], i == 0 ? fBig : fSmall, new Point(x + padX, y + (maxH - sz[i].Height) / 2), c, TF);
                x += cw + gap;
            }
        }
    }

    // ================= Janela principal + bandeja + teclado global =================
    sealed class MainForm : Form
    {
        readonly Engine win = new Engine();
        readonly Engine glob = new Engine();
        Theme th; bool dark; string lang; bool initing = true;
        readonly Dictionary<string, KeyBtn> keys = new Dictionary<string, KeyBtn>();
        Label lblTitle, lblSub, lblMsg;
        ComboBox cmbLang;
        Button btnTheme, btnCopy, btnUndo, btnRedo, btnClear, btnGlobal;
        Panel pOuter, pIn;
        RichTextBox rtb;
        Font fText, fUnder, fPh;
        NotifyIcon tray; ContextMenuStrip menu;
        ToolStripMenuItem mShow, mGlobal, mTop, mAuto, mHelp, mExit;
        ToolTip tip = new ToolTip();
        System.Windows.Forms.Timer timer;
        Icon icOn, icOff;
        bool startHidden, allowExit, handleShown;
        int msgTs, toolsY;
        // global
        bool globalOn; IntPtr hook = IntPtr.Zero; Native.HookProc hookProc; IntPtr hwnd = IntPtr.Zero; IntPtr lastFg = IntPtr.Zero;
        readonly HashSet<string> swallowed = new HashSet<string>();
        int lastComp, lastCommit;

        string T(string k)
        {
            Dictionary<string, string> d; string v;
            if (Data.UI.TryGetValue(lang, out d) && d.TryGetValue(k, out v)) return v;
            return Data.UI["en"][k];
        }

        public MainForm(bool startHidden)
        {
            this.startHidden = startHidden;
            using (Graphics g0 = Graphics.FromHwnd(IntPtr.Zero)) Ui.Dpi = g0.DpiX;
            Settings.Load();
            lang = Settings.Lang;
            if (lang == null || !Data.UI.ContainsKey(lang))
            {
                lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                if (!Data.UI.ContainsKey(lang)) lang = "en";
            }
            string tn = Settings.Theme;
            if (tn == null) tn = SystemIsDark() ? "dark" : "light";
            dark = tn == "dark";
            th = dark ? Theme.Dark() : Theme.Light();
            hookProc = HookCb;

            AutoScaleMode = AutoScaleMode.None;
            Font = new Font("Segoe UI", 9f);
            Text = "Korean 10-key";
            FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = Settings.Top;
            icOff = Ui.MakeIcon(Color.FromArgb(0x6B, 0x72, 0x80), Color.White);
            icOn = Ui.MakeIcon(Color.FromArgb(0x0F, 0x6C, 0xBD), Color.White);
            Icon = icOff;
            ClientSize = new Size(Ui.D(448), Ui.D(638));

            lblTitle = new Label(); lblTitle.Text = "Korean 10-key"; lblTitle.AutoSize = false;
            lblTitle.Font = new Font("Segoe UI Semibold", 13f); lblTitle.TextAlign = ContentAlignment.MiddleLeft;
            lblTitle.SetBounds(Ui.D(18), Ui.D(16), Ui.D(230), Ui.D(32));
            cmbLang = new ComboBox(); cmbLang.DropDownStyle = ComboBoxStyle.DropDownList; cmbLang.FlatStyle = FlatStyle.Flat;
            for (int i = 0; i < Data.Langs.Length; i++) cmbLang.Items.Add(Data.Langs[i][1]);
            cmbLang.SetBounds(Ui.D(448 - 18 - 34 - 8 - 120), Ui.D(18), Ui.D(120), Ui.D(28));
            btnTheme = new Button(); btnTheme.Text = "\u25D0"; btnTheme.TabStop = false;
            btnTheme.SetBounds(Ui.D(448 - 18 - 34), Ui.D(16), Ui.D(34), Ui.D(30));
            lblSub = new Label(); lblSub.AutoSize = false; lblSub.SetBounds(Ui.D(18), Ui.D(52), Ui.D(412), Ui.D(34));

            pOuter = new Panel(); pOuter.SetBounds(Ui.D(18), Ui.D(90), Ui.D(412), Ui.D(124));
            pIn = new Panel(); pIn.SetBounds(1, 1, pOuter.Width - 2, pOuter.Height - 2);
            fText = new Font(Ui.KoFont, 18f); fUnder = new Font(Ui.KoFont, 18f, FontStyle.Underline); fPh = new Font("Segoe UI", 10f);
            rtb = new RichTextBox(); rtb.ReadOnly = true; rtb.BorderStyle = BorderStyle.None; rtb.TabStop = false;
            rtb.ScrollBars = RichTextBoxScrollBars.Vertical; rtb.DetectUrls = false; rtb.ShortcutsEnabled = false;
            rtb.Font = fText; rtb.Cursor = Cursors.Default;
            rtb.SetBounds(Ui.D(12), Ui.D(8), pIn.Width - Ui.D(24), pIn.Height - Ui.D(16));
            pIn.Controls.Add(rtb); pOuter.Controls.Add(pIn);

            toolsY = Ui.D(224);
            btnCopy = MakeTool(); btnUndo = MakeTool(); btnRedo = MakeTool(); btnClear = MakeTool();
            btnCopy.Click += delegate { DoCopy(); };
            btnUndo.Click += delegate { win.Undo(); RenderWin(); };
            btnRedo.Click += delegate { win.Redo(); RenderWin(); };
            btnClear.Click += delegate { win.Clear(); RenderWin(); Say(T("cleared")); };

            BuildPad(Ui.D(268));
            lblMsg = new Label(); lblMsg.AutoSize = false; lblMsg.SetBounds(Ui.D(18), Ui.D(566), Ui.D(412), Ui.D(18));
            btnGlobal = new Button(); btnGlobal.TabStop = false; btnGlobal.SetBounds(Ui.D(18), Ui.D(588), Ui.D(412), Ui.D(34));
            btnGlobal.Click += delegate { SetGlobal(!globalOn); };

            Controls.AddRange(new Control[] { lblTitle, cmbLang, btnTheme, lblSub, pOuter, btnCopy, btnUndo, btnRedo, btnClear, lblMsg, btnGlobal });

            btnTheme.Click += delegate
            {
                dark = !dark; th = dark ? Theme.Dark() : Theme.Light();
                Settings.Theme = dark ? "dark" : "light"; Settings.Save(); ApplyTheme();
            };
            cmbLang.SelectedIndexChanged += delegate
            {
                if (initing) return;
                lang = Data.Langs[cmbLang.SelectedIndex][0];
                Settings.Lang = lang; Settings.Save(); ApplyLang();
                cmbLang.Parent.Focus();
            };

            // tray
            menu = new ContextMenuStrip();
            mShow = new ToolStripMenuItem(); mGlobal = new ToolStripMenuItem(); mTop = new ToolStripMenuItem();
            mAuto = new ToolStripMenuItem(); mHelp = new ToolStripMenuItem(); mExit = new ToolStripMenuItem();
            mShow.Click += delegate { ShowMain(); };
            mGlobal.Click += delegate { SetGlobal(!globalOn); };
            mTop.Click += delegate { TopMost = !TopMost; Settings.Top = TopMost; Settings.Save(); };
            mAuto.Click += delegate { AutoStart.Set(!AutoStart.Get()); };
            mHelp.Click += delegate { ShowHelp(); };
            mExit.Click += delegate { allowExit = true; Close(); };
            menu.Items.AddRange(new ToolStripItem[] { mShow, mGlobal, mTop, mAuto, mHelp, new ToolStripSeparator(), mExit });
            menu.Opening += delegate { mGlobal.Checked = globalOn; mTop.Checked = TopMost; mAuto.Checked = AutoStart.Get(); };
            tray = new NotifyIcon(); tray.Icon = icOff; tray.ContextMenuStrip = menu; tray.Visible = true;
            tray.MouseClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ToggleWindow(); };

            timer = new System.Windows.Forms.Timer(); timer.Interval = 100; timer.Tick += Tick; timer.Start();

            cmbLang.SelectedIndex = IndexOfLang(lang);
            initing = false;
            ApplyLang();
            ApplyTheme();
        }

        static bool SystemIsDark()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (k == null) return false;
                    object v = k.GetValue("AppsUseLightTheme");
                    return v is int && (int)v == 0;
                }
            }
            catch (Exception) { return false; }
        }
        static int IndexOfLang(string l)
        {
            for (int i = 0; i < Data.Langs.Length; i++) if (Data.Langs[i][0] == l) return i;
            return 0;
        }

        Button MakeTool()
        {
            Button b = new Button(); b.TabStop = false; b.UseVisualStyleBackColor = false; return b;
        }

        void BuildPad(int top)
        {
            int cw = Ui.D(97), ch = Ui.D(52), gap = Ui.D(8), left = Ui.D(18);
            Add("NumLock", "Num", new string[] { "123" }, 2, "Plain digits mode", 0, 0, 1, 1, left, top, cw, ch, gap);
            Add("NumpadDivide", "/", Data.CY["NumpadDivide"], 2, "Question mark", 1, 0, 1, 1, left, top, cw, ch, gap);
            Add("NumpadMultiply", "*", Data.CY["NumpadMultiply"], 2, "Exclamation mark", 2, 0, 1, 1, left, top, cw, ch, gap);
            Add("NumpadSubtract", "\u2212", new string[] { "\u232B" }, 2, "Backspace", 3, 0, 1, 1, left, top, cw, ch, gap);
            Add("Numpad7", "7", new string[] { Data.VT["1"] }, 1, "", 0, 1, 1, 1, left, top, cw, ch, gap);
            Add("Numpad8", "8", new string[] { Data.VT["2"] }, 1, "", 1, 1, 1, 1, left, top, cw, ch, gap);
            Add("Numpad9", "9", new string[] { Data.VT["3"] }, 1, "", 2, 1, 1, 1, left, top, cw, ch, gap);
            Add("NumpadAdd", "+", new string[] { "\u2423" }, 2, "Space", 3, 1, 1, 2, left, top, cw, ch, gap);
            Add("Numpad4", "4", Data.CY["Numpad4"], 0, "", 0, 2, 1, 1, left, top, cw, ch, gap);
            Add("Numpad5", "5", Data.CY["Numpad5"], 0, "", 1, 2, 1, 1, left, top, cw, ch, gap);
            Add("Numpad6", "6", Data.CY["Numpad6"], 0, "", 2, 2, 1, 1, left, top, cw, ch, gap);
            Add("Numpad1", "1", Data.CY["Numpad1"], 0, "", 0, 3, 1, 1, left, top, cw, ch, gap);
            Add("Numpad2", "2", Data.CY["Numpad2"], 0, "", 1, 3, 1, 1, left, top, cw, ch, gap);
            Add("Numpad3", "3", Data.CY["Numpad3"], 0, "", 2, 3, 1, 1, left, top, cw, ch, gap);
            Add("NumpadEnter", "Enter", new string[] { "\u23CE" }, 2, "Enter, new line", 3, 3, 1, 2, left, top, cw, ch, gap);
            Add("Numpad0", "0", Data.CY["Numpad0"], 0, "", 0, 4, 2, 1, left, top, cw, ch, gap);
            Add("NumpadDecimal", ".", Data.CY["NumpadDecimal"], 2, "Period and comma", 2, 4, 1, 1, left, top, cw, ch, gap);
        }

        void Add(string code, string num, string[] glyphs, int kind, string name, int c, int r, int cs, int rs,
                 int left, int top, int cw, int ch, int gap)
        {
            KeyBtn k = new KeyBtn(code, num, glyphs, kind);
            k.Tip = name;
            k.Th = th;
            k.SetBounds(left + c * (cw + gap), top + r * (ch + gap), cs * cw + (cs - 1) * gap, rs * ch + (rs - 1) * gap);
            k.Clicked = delegate(KeyBtn b) { PressWin(b.Code, false); };
            keys[code] = k;
            Controls.Add(k);
        }

        // ---------- idioma / tema ----------
        void ApplyLang()
        {
            lblSub.Text = T("sub");
            btnCopy.Text = T("copy"); btnUndo.Text = T("undo"); btnRedo.Text = T("redo"); btnClear.Text = T("clear");
            tip.SetToolTip(btnTheme, T("theme")); cmbLang.AccessibleName = T("lang");
            Dictionary<string, string> names; if (!Data.Names.TryGetValue(lang, out names)) names = Data.Names["en"];
            foreach (KeyBtn k in keys.Values)
            {
                string nm = k.Tip; string tr;
                if (nm.Length > 0) { if (names.TryGetValue(nm, out tr)) nm = tr; }
                else nm = T("key") + " " + k.Num + ": " + string.Join(" ", k.Glyphs);
                k.AccessibleName = nm; tip.SetToolTip(k, nm);
            }
            mShow.Text = T("show");
            mTop.Text = T("top"); mAuto.Text = T("auto"); mHelp.Text = T("gt"); mExit.Text = T("exit");
            mGlobal.Text = T("gOn").Split(new char[] { ':', '\uFF1A' })[0] + "  (Ctrl+Alt+H)";
            int x = Ui.D(18);
            foreach (Button b in new Button[] { btnCopy, btnUndo, btnRedo, btnClear })
            {
                int w = TextRenderer.MeasureText(b.Text, b.Font).Width + Ui.D(26);
                b.SetBounds(x, toolsY, w, Ui.D(32)); x += w + Ui.D(8);
            }
            UpdateGlobalUi();
            RenderWin();
        }

        void StyleBtn(Button b, bool accent)
        {
            b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 1; b.FlatAppearance.BorderColor = th.Line;
            b.BackColor = accent ? th.Acc : th.UKey; b.ForeColor = accent ? th.AccInk : th.Ink;
        }

        void ApplyTheme()
        {
            BackColor = th.Bg; ForeColor = th.Ink;
            lblTitle.ForeColor = th.Ink; lblSub.ForeColor = th.Dim; lblMsg.ForeColor = th.Dim;
            lblTitle.BackColor = th.Bg; lblSub.BackColor = th.Bg; lblMsg.BackColor = th.Bg;
            pOuter.BackColor = th.Line; pIn.BackColor = th.Screen; rtb.BackColor = th.Screen; rtb.ForeColor = th.Ink;
            cmbLang.BackColor = th.UKey; cmbLang.ForeColor = th.Ink;
            StyleBtn(btnTheme, false); StyleBtn(btnCopy, true); StyleBtn(btnUndo, false); StyleBtn(btnRedo, false); StyleBtn(btnClear, false);
            foreach (KeyBtn k in keys.Values) { k.Th = th; k.Invalidate(); }
            if (IsHandleCreated) DarkTitle();
            UpdateGlobalUi();
            RenderWin();
        }

        void DarkTitle()
        {
            try
            {
                int v = dark ? 1 : 0;
                Native.DwmSetWindowAttribute(Handle, 20, ref v, 4);
                Native.DwmSetWindowAttribute(Handle, 19, ref v, 4);
            }
            catch (Exception) { }
        }

        // ---------- modo janela ----------
        void RenderWin()
        {
            string comp = Engine.Comp(win.S);
            string full = win.S.Text + comp;
            if (full.Length == 0)
            {
                rtb.Text = T("ph");
                rtb.SelectAll(); rtb.SelectionFont = fPh; rtb.SelectionColor = th.Dim; rtb.Select(0, 0);
            }
            else
            {
                rtb.Text = full;
                rtb.SelectAll(); rtb.SelectionFont = fText; rtb.SelectionColor = th.Ink;
                if (comp.Length > 0) { rtb.Select(win.S.Text.Length, comp.Length); rtb.SelectionFont = fUnder; rtb.SelectionColor = th.Acc; }
                rtb.Select(rtb.TextLength, 0); rtb.ScrollToCaret();
            }
            bool empty = full.Length == 0;
            btnUndo.Enabled = win.H.Count > 0; btnRedo.Enabled = win.R.Count > 0;
            btnCopy.Enabled = !empty; btnClear.Enabled = !empty;
            keys["NumLock"].Toggled = win.Digits; keys["NumLock"].Invalidate();
        }

        void PressWin(string code, bool flash)
        {
            if (code == "NumLock") { win.ToggleDigits(); Say(T(win.Digits ? "digits" : "hangul")); }
            else win.Press(code);
            if (flash) FlashKey(code);
            RenderWin();
        }

        void DoCopy()
        {
            string t = win.Full();
            if (t.Length == 0) return;
            try { Clipboard.SetText(t.Replace("\n", "\r\n")); Say(T("copied")); } catch (Exception) { }
        }

        void Say(string s) { lblMsg.Text = s; msgTs = Environment.TickCount; }

        void FlashKey(string code)
        {
            KeyBtn k;
            if (code == "Space") code = "NumpadAdd"; else if (code == "Enter") code = "NumpadEnter"; else if (code == "Backspace") code = "NumpadSubtract";
            if (keys.TryGetValue(code, out k) && code != "NumLock") { k.Flash = true; k.FlashTs = Environment.TickCount; k.Invalidate(); }
        }

        int MarkOf(Engine e, string code, int now)
        {
            if (e.MarkCode == code && unchecked(now - e.MarkTs) <= 1000) return e.MarkIdx;
            return -1;
        }

        void Tick(object sender, EventArgs e)
        {
            int now = Environment.TickCount;
            if (Visible)
            {
                foreach (KeyBtn k in keys.Values)
                {
                    if (k.Flash && unchecked(now - k.FlashTs) > 130) { k.Flash = false; k.Invalidate(); }
                    int want = -1;
                    if (k.Glyphs.Length > 1) { want = MarkOf(win, k.Code, now); if (want < 0) want = MarkOf(glob, k.Code, now); }
                    if (k.Cur != want) { k.Cur = want; k.Invalidate(); }
                }
                if (lblMsg.Text.Length > 0 && unchecked(now - msgTs) > 1600) lblMsg.Text = "";
            }
            if (globalOn)
            {
                IntPtr fg = Native.GetForegroundWindow();
                if (fg != lastFg) { lastFg = fg; ResetGlobalState(); }
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys k = keyData & Keys.KeyCode; Keys mods = keyData & Keys.Modifiers;
            if ((mods & Keys.Control) != 0 && (mods & Keys.Alt) == 0)
            {
                if (k == Keys.Z) { if ((mods & Keys.Shift) != 0) win.Redo(); else win.Undo(); RenderWin(); return true; }
                if (k == Keys.Y) { win.Redo(); RenderWin(); return true; }
                return base.ProcessCmdKey(ref msg, keyData);
            }
            if ((mods & (Keys.Control | Keys.Alt)) != 0) return base.ProcessCmdKey(ref msg, keyData);
            long lp = msg.LParam.ToInt64();
            int scan = (int)((lp >> 16) & 0xFF); bool ext = (lp & 0x01000000) != 0; bool repeat = (lp & 0x40000000) != 0;
            string code = null;
            if (k == Keys.NumLock) code = "NumLock";
            else
            {
                code = KeyMap.Map(scan, ext);
                if (code == null && !(ActiveControl is ComboBox))
                {
                    if (k == Keys.Space) code = "Space"; else if (k == Keys.Return) code = "Enter"; else if (k == Keys.Back) code = "Backspace";
                }
            }
            if (code == null) return base.ProcessCmdKey(ref msg, keyData);
            if (repeat && code != "NumpadSubtract" && code != "Backspace") return true;
            PressWin(code, true);
            return true;
        }

        // ---------- janela / bandeja ----------
        void ShowMain() { Show(); WindowState = FormWindowState.Normal; Activate(); }
        void ToggleWindow() { if (Visible) Hide(); else ShowMain(); }
        void ShowHelp() { MessageBox.Show(this, string.Join("\n\n", Data.Guide[lang]), T("gt"), MessageBoxButtons.OK, MessageBoxIcon.Information); }

        protected override void SetVisibleCore(bool value)
        {
            if (!handleShown)
            {
                handleShown = true;
                if (startHidden) { if (!IsHandleCreated) CreateHandle(); value = false; }
            }
            base.SetVisibleCore(value);
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            hwnd = Handle;
            Native.RegisterHotKey(Handle, 1, 0x4003, 0x48); // Ctrl+Alt+H
            DarkTitle();
        }
        protected override void OnHandleDestroyed(EventArgs e)
        {
            Native.UnregisterHotKey(hwnd, 1);
            base.OnHandleDestroyed(e);
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312 && m.WParam.ToInt32() == 1) SetGlobal(!globalOn);
            base.WndProc(ref m);
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!allowExit && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); return; }
            RemoveHook(); timer.Stop(); tray.Visible = false; tray.Dispose();
            base.OnFormClosing(e);
        }

        // ---------- teclado global ----------
        void UpdateGlobalUi()
        {
            string s = T(globalOn ? "gOn" : "gOff");
            btnGlobal.Text = s + "   \u00B7   Ctrl+Alt+H";
            StyleBtn(btnGlobal, globalOn);
            mGlobal.Checked = globalOn;
            string tt = s + (globalOn && glob.Digits ? " \u00B7 123" : "");
            tray.Text = tt.Length > 63 ? tt.Substring(0, 63) : tt;
            tray.Icon = globalOn ? icOn : icOff;
            Icon = globalOn ? icOn : icOff;
        }

        void SetGlobal(bool on)
        {
            if (on == globalOn) return;
            if (on)
            {
                if (!InstallHook()) { Say("Could not install keyboard hook"); return; }
                globalOn = true; swallowed.Clear(); ResetGlobalState(); lastFg = Native.GetForegroundWindow();
            }
            else { RemoveHook(); globalOn = false; swallowed.Clear(); ResetGlobalState(); }
            UpdateGlobalUi();
        }

        bool InstallHook()
        {
            if (hook != IntPtr.Zero) return true;
            IntPtr hMod;
            using (Process p = Process.GetCurrentProcess())
            using (ProcessModule mod = p.MainModule) hMod = Native.GetModuleHandle(mod.ModuleName);
            hook = Native.SetWindowsHookEx(13, hookProc, hMod, 0);
            return hook != IntPtr.Zero;
        }
        void RemoveHook() { if (hook != IntPtr.Zero) { Native.UnhookWindowsHookEx(hook); hook = IntPtr.Zero; } }

        void ResetGlobalState()
        {
            glob.S = new State(); glob.H.Clear(); glob.R.Clear(); glob.CancelCycle(); lastComp = 0; lastCommit = 0;
        }

        IntPtr HookCb(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && globalOn)
            {
                try
                {
                    int msg = wParam.ToInt32();
                    KBDLL k = (KBDLL)Marshal.PtrToStructure(lParam, typeof(KBDLL));
                    if ((k.flags & 0x10) == 0)
                    {
                        bool down = msg == 0x100 || msg == 0x104, up = msg == 0x101 || msg == 0x105;
                        if (HandleKey(k, down, up)) return (IntPtr)1;
                    }
                }
                catch (Exception) { }
            }
            return Native.CallNextHookEx(hook, nCode, wParam, lParam);
        }

        static bool IsModifier(uint vk)
        {
            return vk == 0x10 || vk == 0x11 || vk == 0x12 || (vk >= 0xA0 && vk <= 0xA5) || vk == 0x5B || vk == 0x5C || vk == 0x14 || vk == 0x91;
        }

        bool HandleKey(KBDLL k, bool down, bool up)
        {
            if (hwnd != IntPtr.Zero && Native.GetForegroundWindow() == hwnd) return false; // na própria janela, trata ela
            bool ext = (k.flags & 1) != 0;
            string code = (k.vkCode == 0x90) ? "NumLock" : KeyMap.Map((int)k.scanCode, ext);
            if (up)
            {
                if (code != null && swallowed.Remove(code)) return true;
                return false;
            }
            if (!down) return false;
            if (code == null)
            {
                if (!IsModifier(k.vkCode)) ResetGlobalState();
                return false;
            }
            bool mod = (Native.GetAsyncKeyState(0x11) & 0x8000) != 0 || (Native.GetAsyncKeyState(0x12) & 0x8000) != 0
                    || (Native.GetAsyncKeyState(0x5B) & 0x8000) != 0 || (Native.GetAsyncKeyState(0x5C) & 0x8000) != 0;
            if (mod) { ResetGlobalState(); return false; }
            bool repeat = swallowed.Contains(code);
            swallowed.Add(code);
            if (code == "NumLock")
            {
                if (!repeat) { glob.ToggleDigits(); ResetGlobalState(); UpdateGlobalUi(); }
                return true;
            }
            if (repeat && code != "NumpadSubtract") return true;
            GlobalPress(code);
            return true;
        }

        void GlobalPress(string code)
        {
            State s = glob.S;
            if (code == "NumpadSubtract" && !glob.Digits && s.L.Length == 0 && s.V.Length == 0 && s.T.Length == 0)
            {
                Sender.Key(0x08); ResetGlobalState(); return;
            }
            glob.S.Text = "";
            bool cyc = glob.Press(code);
            string commit = glob.S.Text;
            string comp = Engine.Comp(glob.S);
            int del = cyc ? lastCommit + lastComp : lastComp;
            Sender.Backspaces(del);
            Sender.Text(commit + comp);
            lastCommit = commit.Length; lastComp = comp.Length;
            glob.S.Text = "";
            while (glob.H.Count > 4) glob.H.RemoveAt(0);
            glob.R.Clear();
            if (Visible) FlashKey(code);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            bool created;
            using (System.Threading.Mutex m = new System.Threading.Mutex(true, "Local\\Teclado10Key.Single", out created))
            {
                if (!created) return;
                try { Native.SetProcessDPIAware(); } catch (Exception) { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                bool hidden = false;
                foreach (string a in args)
                    if (a.Equals("/min", StringComparison.OrdinalIgnoreCase) || a.Equals("-min", StringComparison.OrdinalIgnoreCase)) hidden = true;
                Application.Run(new MainForm(hidden));
                GC.KeepAlive(m);
            }
        }
    }
}
