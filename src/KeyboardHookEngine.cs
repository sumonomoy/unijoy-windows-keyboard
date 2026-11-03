using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace UniBijoyKeyboard
{
    public sealed class KeyboardHookEngine : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const int WM_INPUT = 0x00FF;
        private const int WM_HOTKEY = 0x0312;
        private const int WM_APP_MODE_CHANGED = 0x8001;

        private const uint LLKHF_ALTDOWN = 0x0020;

        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_NOREPEAT = 0x4000;
        private const int HOTKEY_ID_CTRL_SPACE = 0x5542;

        private const uint RIDEV_INPUTSINK = 0x00000100;
        private const uint RID_INPUT = 0x10000003;
        private const uint RIM_TYPEKEYBOARD = 1;
        private const ushort RI_KEY_BREAK = 0x0001;

        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;

        private const int VK_BACK = 0x08;
        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12; // Alt
        private const int VK_SPACE = 0x20;
        private const int VK_NUMLOCK = 0x90;
        private const int VK_LSHIFT = 0xA0;
        private const int VK_RSHIFT = 0xA1;
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;
        private const int VK_LMENU = 0xA4;
        private const int VK_RMENU = 0xA5;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;

        private static readonly IntPtr MagicExtraInfo = new IntPtr(0x4156524F); // 'AVRO'

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private delegate void WinEventDelegate(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime);

        private readonly LowLevelKeyboardProc _proc;
        private readonly WinEventDelegate _winEventProc;
        private IntPtr _hookId = IntPtr.Zero;
        private IntPtr _winEventHookId = IntPtr.Zero;
        private IntPtr _moduleHandle = IntPtr.Zero;
        private HotkeyWindow _hotkeyWindow;
        private AppConfig _config;
        private AvroLayout _layout;
        private bool _isBengaliMode;
        private bool _lastOutputWasHasanta;

        // Track Ctrl key state directly from hook & raw input streams
        private bool _hookCtrlDown;
        private long _hookCtrlDownTick;
        private bool _rawCtrlDown;
        private bool _rawAltDown;
        private bool _rawShiftDown;

        // Repeat guard for Ctrl+Space while Space remains held down
        private bool _ctrlSpaceHeld;
        private long _lastSpaceDownTick;
        private long _lastHotkeyToggleTick;

        public event EventHandler ModeChanged;
        public event EventHandler ShutdownRequested;

        private static readonly Dictionary<string, string> KarToFullVowelMap = new Dictionary<string, string>
        {
            { "\u09BE", "\u0986" }, // া -> আ
            { "\u09BF", "\u0987" }, // ি -> ই
            { "\u09C0", "\u0988" }, // ী -> ঈ
            { "\u09C1", "\u0989" }, // ু -> উ
            { "\u09C2", "\u098A" }, // ূ -> ঊ
            { "\u09C3", "\u098B" }, // ৃ -> ঋ
            { "\u09C7", "\u098F" }, // ে -> এ
            { "\u09C8", "\u0990" }, // ৈ -> ঐ
            { "\u09CB", "\u0993" }, // ো -> ও
            { "\u09CC", "\u0994" }, // ৌ -> ঔ
            { "\u0985", "\u0985" }  // অ -> অ (if Hasanta was pressed before Shift+F)
        };

        public bool IsHookInstalled
        {
            get { return _hookId != IntPtr.Zero; }
        }

        public bool IsRawInputRegistered
        {
            get { return _hotkeyWindow != null && _hotkeyWindow.IsRawInputRegistered; }
        }

        private sealed class HotkeyWindow : NativeWindow, IDisposable
        {
            private readonly KeyboardHookEngine _owner;
            private readonly uint _shutdownMsg;
            private bool _hotkeyRegistered;
            private bool _rawInputRegistered;

            public bool IsRawInputRegistered
            {
                get { return _rawInputRegistered; }
            }

            public HotkeyWindow(KeyboardHookEngine owner)
            {
                _owner = owner;
                _shutdownMsg = RegisterWindowMessage("UniBijoyKeyboard_CleanShutdown_Msg");

                CreateHandle(new CreateParams());

                // 1. Register Raw Input sink (always succeeds even if another app registered Ctrl+Space)
                var rid = new RAWINPUTDEVICE[1];
                rid[0].usUsagePage = 0x01; // Generic Desktop Controls
                rid[0].usUsage = 0x06;     // Keyboard
                rid[0].dwFlags = RIDEV_INPUTSINK;
                rid[0].hwndTarget = Handle;
                _rawInputRegistered = RegisterRawInputDevices(rid, 1, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));

                // 2. Also attempt RegisterHotKey if available
                _hotkeyRegistered = RegisterHotKey(Handle, HOTKEY_ID_CTRL_SPACE, MOD_CONTROL | MOD_NOREPEAT, (uint)VK_SPACE);
                if (!_hotkeyRegistered)
                {
                    _hotkeyRegistered = RegisterHotKey(Handle, HOTKEY_ID_CTRL_SPACE, MOD_CONTROL, (uint)VK_SPACE);
                }
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_APP_MODE_CHANGED)
                {
                    _owner.NotifyModeChangedOnUiThread();
                    return;
                }

                if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID_CTRL_SPACE)
                {
                    _owner.OnHotkeyTriggered();
                    return;
                }

                if (m.Msg == WM_INPUT)
                {
                    _owner.ProcessRawInput(m.LParam);
                }

                if (_shutdownMsg != 0 && (uint)m.Msg == _shutdownMsg)
                {
                    _owner.NotifyShutdownRequested();
                    return;
                }

                base.WndProc(ref m);
            }

            public void Dispose()
            {
                if (_hotkeyRegistered && Handle != IntPtr.Zero)
                {
                    UnregisterHotKey(Handle, HOTKEY_ID_CTRL_SPACE);
                    _hotkeyRegistered = false;
                }
                if (Handle != IntPtr.Zero)
                {
                    DestroyHandle();
                }
            }
        }

        public KeyboardHookEngine(AppConfig config, AvroLayout layout)
        {
            if (config == null) throw new ArgumentNullException("config");
            if (layout == null) throw new ArgumentNullException("layout");
            _config = config;
            _layout = layout;
            _isBengaliMode = _config.StartInBengaliMode;
            _proc = HookCallback;
            _winEventProc = OnForegroundWindowChanged;
        }

        public bool IsBengaliMode
        {
            get { return _isBengaliMode; }
            set
            {
                if (_isBengaliMode != value)
                {
                    _isBengaliMode = value;
                    _lastOutputWasHasanta = false;
                    _ctrlSpaceHeld = false;
                    PostModeChangedToUiThread();
                }
            }
        }

        private void PostModeChangedToUiThread()
        {
            if (_hotkeyWindow != null && _hotkeyWindow.Handle != IntPtr.Zero)
            {
                PostMessage(_hotkeyWindow.Handle, WM_APP_MODE_CHANGED, IntPtr.Zero, IntPtr.Zero);
            }
            else
            {
                NotifyModeChangedOnUiThread();
            }
        }

        private void NotifyModeChangedOnUiThread()
        {
            EventHandler handler = ModeChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void NotifyShutdownRequested()
        {
            EventHandler handler = ShutdownRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        public AvroLayout CurrentLayout
        {
            get { return _layout; }
            set
            {
                if (value != null)
                {
                    _layout = value;
                    _lastOutputWasHasanta = false;
                }
            }
        }

        public void UpdateConfig(AppConfig config)
        {
            if (config == null) throw new ArgumentNullException("config");
            _config = config;
        }

        public void ToggleMode()
        {
            IsBengaliMode = !IsBengaliMode;
        }

        private void OnHotkeyTriggered()
        {
            long now = GetTick();
            if (now - _lastHotkeyToggleTick < 180)
            {
                return;
            }
            _lastHotkeyToggleTick = now;
            ToggleMode();
        }

        public void Start()
        {
            if (_hotkeyWindow == null)
            {
                _hotkeyWindow = new HotkeyWindow(this);
            }

            if (_moduleHandle == IntPtr.Zero)
            {
                using (Process curProcess = Process.GetCurrentProcess())
                using (ProcessModule curModule = curProcess.MainModule)
                {
                    _moduleHandle = GetModuleHandle(curModule.ModuleName);
                }
            }

            if (_hookId == IntPtr.Zero)
            {
                _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, _moduleHandle, 0);
            }

            if (_winEventHookId == IntPtr.Zero)
            {
                _winEventHookId = SetWinEventHook(
                    EVENT_SYSTEM_FOREGROUND,
                    EVENT_SYSTEM_FOREGROUND,
                    IntPtr.Zero,
                    _winEventProc,
                    0,
                    0,
                    WINEVENT_OUTOFCONTEXT);
            }
        }

        private void OnForegroundWindowChanged(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime)
        {
            // Keep language mode universal across all application switches!
            // Reset transient composition state and refresh our hook to the top of the hook chain
            _lastOutputWasHasanta = false;
            _ctrlSpaceHeld = false;

            if (_hookId != IntPtr.Zero && _moduleHandle != IntPtr.Zero)
            {
                IntPtr oldHook = _hookId;
                IntPtr newHook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, _moduleHandle, 0);
                if (newHook != IntPtr.Zero)
                {
                    _hookId = newHook;
                    UnhookWindowsHookEx(oldHook);
                }
            }
        }

        private void ProcessRawInput(IntPtr hRawInput)
        {
            uint dwSize = 0;
            uint headerSize = (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER));
            GetRawInputData(hRawInput, RID_INPUT, IntPtr.Zero, ref dwSize, headerSize);
            if (dwSize == 0 || dwSize > 1024)
                return;

            IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);
            try
            {
                if (GetRawInputData(hRawInput, RID_INPUT, buffer, ref dwSize, headerSize) != dwSize)
                    return;

                RAWINPUTHEADER header = (RAWINPUTHEADER)Marshal.PtrToStructure(buffer, typeof(RAWINPUTHEADER));
                if (header.dwType != RIM_TYPEKEYBOARD)
                    return;

                IntPtr kbdPtr = new IntPtr(buffer.ToInt64() + (int)headerSize);
                RAWKEYBOARD kbd = (RAWKEYBOARD)Marshal.PtrToStructure(kbdPtr, typeof(RAWKEYBOARD));

                bool isBreak = (kbd.Flags & RI_KEY_BREAK) != 0;
                int vKey = kbd.VKey;

                if (vKey == VK_CONTROL || vKey == VK_LCONTROL || vKey == VK_RCONTROL)
                {
                    _rawCtrlDown = !isBreak;
                    if (!isBreak)
                    {
                        _hookCtrlDown = true;
                        _hookCtrlDownTick = GetTick();
                    }
                    else
                    {
                        _hookCtrlDown = false;
                        _ctrlSpaceHeld = false;
                    }
                }
                else if (vKey == VK_MENU || vKey == VK_LMENU || vKey == VK_RMENU)
                {
                    _rawAltDown = !isBreak;
                }
                else if (vKey == VK_SHIFT || vKey == VK_LSHIFT || vKey == VK_RSHIFT)
                {
                    _rawShiftDown = !isBreak;
                }
                else if (vKey == VK_SPACE)
                {
                    if (isBreak)
                    {
                        _ctrlSpaceHeld = false;
                    }
                    else
                    {
                        bool ctrl = _rawCtrlDown || _hookCtrlDown || IsKeyDown(VK_CONTROL) || IsKeyDown(VK_LCONTROL) || IsKeyDown(VK_RCONTROL);
                        bool alt = _rawAltDown && (IsKeyDown(VK_MENU) || IsKeyDown(VK_LMENU) || IsKeyDown(VK_RMENU));
                        bool shift = _rawShiftDown && (IsKeyDown(VK_SHIFT) || IsKeyDown(VK_LSHIFT) || IsKeyDown(VK_RSHIFT));
                        if (ctrl && !alt && !shift)
                        {
                            long now = GetTick();
                            if (!_ctrlSpaceHeld || (now - _lastSpaceDownTick > 400))
                            {
                                _ctrlSpaceHeld = true;
                                _lastSpaceDownTick = now;
                                OnHotkeyTriggered();
                            }
                        }
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public void Stop()
        {
            if (_winEventHookId != IntPtr.Zero)
            {
                UnhookWinEvent(_winEventHookId);
                _winEventHookId = IntPtr.Zero;
            }

            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }

            if (_hotkeyWindow != null)
            {
                _hotkeyWindow.Dispose();
                _hotkeyWindow = null;
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                bool isKeyDown = (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN);
                bool isKeyUp = (msg == WM_KEYUP || msg == WM_SYSKEYUP);

                KBDLLHOOKSTRUCT info = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));

                // Ignore synthetic events injected by our own SendInput calls
                if (info.dwExtraInfo == MagicExtraInfo)
                {
                    return CallNextHookEx(_hookId, nCode, wParam, lParam);
                }

                int vkCode = (int)info.vkCode;

                // Track Ctrl directly in the hook
                if (vkCode == VK_CONTROL || vkCode == VK_LCONTROL || vkCode == VK_RCONTROL)
                {
                    if (isKeyDown)
                    {
                        _hookCtrlDown = true;
                        _hookCtrlDownTick = GetTick();
                    }
                    else if (isKeyUp)
                    {
                        _hookCtrlDown = false;
                        _ctrlSpaceHeld = false;
                    }
                    return CallNextHookEx(_hookId, nCode, wParam, lParam);
                }

                if (isKeyUp)
                {
                    if (vkCode == VK_SPACE)
                    {
                        if (_ctrlSpaceHeld)
                        {
                            _ctrlSpaceHeld = false;
                            return (IntPtr)1;
                        }
                    }
                    return CallNextHookEx(_hookId, nCode, wParam, lParam);
                }

                if (isKeyDown)
                {
                    // Ignore standalone modifier key presses
                    if (IsModifierKey(vkCode))
                    {
                        return CallNextHookEx(_hookId, nCode, wParam, lParam);
                    }

                    if (vkCode != VK_SPACE)
                    {
                        _ctrlSpaceHeld = false;
                    }

                    long now = GetTick();
                    bool lCtrlDown = IsKeyDown(VK_LCONTROL);
                    bool rCtrlDown = IsKeyDown(VK_RCONTROL);
                    bool asyncCtrl = lCtrlDown || rCtrlDown || IsKeyDown(VK_CONTROL) ||
                                     ((GetKeyState(VK_CONTROL) & 0x8000) != 0);
                    bool hookCtrl = (_hookCtrlDown && (now - _hookCtrlDownTick < 8000)) || _rawCtrlDown;

                    bool lAltDown = IsKeyDown(VK_LMENU);
                    bool rAltDown = IsKeyDown(VK_RMENU);
                    bool flagAltDown = (info.flags & LLKHF_ALTDOWN) != 0;
                    bool altDown = flagAltDown || lAltDown || rAltDown || IsKeyDown(VK_MENU);

                    bool shiftDown = IsKeyDown(VK_SHIFT) || IsKeyDown(VK_LSHIFT) || IsKeyDown(VK_RSHIFT);
                    bool winDown = IsKeyDown(VK_LWIN) || IsKeyDown(VK_RWIN);

                    // 1. ONLY Ctrl + Space toggles language mode universally (EN <-> বাং)
                    if (vkCode == VK_SPACE && (asyncCtrl || hookCtrl) && !flagAltDown && !shiftDown && !winDown)
                    {
                        if (!_ctrlSpaceHeld || (now - _lastSpaceDownTick > 400))
                        {
                            _ctrlSpaceHeld = true;
                            _lastSpaceDownTick = now;
                            OnHotkeyTriggered();
                        }
                        _lastSpaceDownTick = now;
                        return (IntPtr)1;
                    }

                    // 2. If in English mode, pass through immediately
                    if (!_isBengaliMode)
                    {
                        return CallNextHookEx(_hookId, nCode, wParam, lParam);
                    }

                    // 3. In Bengali mode: pass through Windows key shortcuts
                    if (winDown)
                    {
                        _lastOutputWasHasanta = false;
                        return CallNextHookEx(_hookId, nCode, wParam, lParam);
                    }

                    // Determine if AltGr is active:
                    // - Right Alt (AltGr on US/International keyboards)
                    // - Or Ctrl + Alt held together
                    // - Or Left Alt if enabled in config
                    bool isAltGr = rAltDown || (asyncCtrl && altDown) || (_config.LeftAltAsAltGr && lAltDown);

                    // If Ctrl is held WITHOUT Alt (e.g. Ctrl+C, Ctrl+V, Ctrl+Z, Ctrl+A, Ctrl+S), pass through!
                    if (asyncCtrl && !altDown)
                    {
                        _lastOutputWasHasanta = false;
                        return CallNextHookEx(_hookId, nCode, wParam, lParam);
                    }

                    // If Left Alt is held alone (without Ctrl and without LeftAltAsAltGr), pass through (e.g. Alt+Tab, Alt+F4)
                    if (altDown && !isAltGr)
                    {
                        _lastOutputWasHasanta = false;
                        return CallNextHookEx(_hookId, nCode, wParam, lParam);
                    }

                    // 4. Look up key in current AvroLayout
                    KeyMappingEntry entry;
                    if (_layout != null && _layout.KeyMap.TryGetValue(vkCode, out entry))
                    {
                        if (entry.IsNumpad)
                        {
                            bool numLockOn = (GetKeyState(VK_NUMLOCK) & 1) != 0;
                            if (!_config.BengaliNumpad || !numLockOn)
                            {
                                _lastOutputWasHasanta = false;
                                return CallNextHookEx(_hookId, nCode, wParam, lParam);
                            }
                        }

                        string outputText;
                        if (isAltGr)
                        {
                            outputText = shiftDown ? entry.ShiftAltGr : entry.AltGr;
                        }
                        else
                        {
                            outputText = shiftDown ? entry.Shift : entry.Normal;
                        }

                        if (!string.IsNullOrEmpty(outputText))
                        {
                            string fullVowel;
                            if (_config.SmartVowelForming &&
                                _lastOutputWasHasanta &&
                                KarToFullVowelMap.TryGetValue(outputText, out fullVowel))
                            {
                                SendBackspaceAndUnicodeString(fullVowel, isAltGr);
                                _lastOutputWasHasanta = false;
                                return (IntPtr)1;
                            }

                            SendUnicodeString(outputText, isAltGr);
                            _lastOutputWasHasanta = (outputText == "\u09CD"); // U+09CD = ্ (Hasanta)
                            return (IntPtr)1;
                        }
                        else if (isAltGr)
                        {
                            return (IntPtr)1;
                        }
                    }
                    else
                    {
                        _lastOutputWasHasanta = false;
                    }
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private static long GetTick()
        {
            return (long)(Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency);
        }

        private static bool IsModifierKey(int vk)
        {
            return vk == VK_SHIFT || vk == VK_LSHIFT || vk == VK_RSHIFT ||
                   vk == VK_CONTROL || vk == VK_LCONTROL || vk == VK_RCONTROL ||
                   vk == VK_MENU || vk == VK_LMENU || vk == VK_RMENU ||
                   vk == VK_LWIN || vk == VK_RWIN;
        }

        private static bool IsKeyDown(int vk)
        {
            return (GetAsyncKeyState(vk) & 0x8000) != 0;
        }

        private static void SendBackspaceAndUnicodeString(string text, bool releaseAltGrModifiers)
        {
            var inputs = new List<INPUT>();

            if (releaseAltGrModifiers)
            {
                AddModifierReleaseInputs(inputs);
            }

            inputs.Add(CreateVirtualKeyInput((ushort)VK_BACK, false));
            inputs.Add(CreateVirtualKeyInput((ushort)VK_BACK, true));

            foreach (char c in text)
            {
                inputs.Add(CreateUnicodeInput(c, false));
                inputs.Add(CreateUnicodeInput(c, true));
            }

            if (inputs.Count > 0)
            {
                INPUT[] arr = inputs.ToArray();
                SendInput((uint)arr.Length, arr, Marshal.SizeOf(typeof(INPUT)));
            }
        }

        private static void SendUnicodeString(string text, bool releaseAltGrModifiers)
        {
            if (string.IsNullOrEmpty(text))
                return;

            var inputs = new List<INPUT>(text.Length * 2 + 4);

            if (releaseAltGrModifiers)
            {
                AddModifierReleaseInputs(inputs);
            }

            foreach (char c in text)
            {
                inputs.Add(CreateUnicodeInput(c, false));
                inputs.Add(CreateUnicodeInput(c, true));
            }

            INPUT[] arr = inputs.ToArray();
            SendInput((uint)arr.Length, arr, Marshal.SizeOf(typeof(INPUT)));
        }

        private static void AddModifierReleaseInputs(List<INPUT> inputs)
        {
            if (IsKeyDown(VK_RMENU)) inputs.Add(CreateVirtualKeyInput((ushort)VK_RMENU, true));
            if (IsKeyDown(VK_LMENU)) inputs.Add(CreateVirtualKeyInput((ushort)VK_LMENU, true));
            if (IsKeyDown(VK_MENU)) inputs.Add(CreateVirtualKeyInput((ushort)VK_MENU, true));
            if (IsKeyDown(VK_LCONTROL)) inputs.Add(CreateVirtualKeyInput((ushort)VK_LCONTROL, true));
            if (IsKeyDown(VK_RCONTROL)) inputs.Add(CreateVirtualKeyInput((ushort)VK_RCONTROL, true));
            if (IsKeyDown(VK_CONTROL)) inputs.Add(CreateVirtualKeyInput((ushort)VK_CONTROL, true));
        }

        private static INPUT CreateUnicodeInput(char ch, bool keyUp)
        {
            return new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = ch,
                        dwFlags = KEYEVENTF_UNICODE | (keyUp ? KEYEVENTF_KEYUP : 0u),
                        time = 0,
                        dwExtraInfo = MagicExtraInfo
                    }
                }
            };
        }

        private static INPUT CreateVirtualKeyInput(ushort vk, bool keyUp)
        {
            return new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = vk,
                        wScan = 0,
                        dwFlags = keyUp ? KEYEVENTF_KEYUP : 0u,
                        time = 0,
                        dwExtraInfo = MagicExtraInfo
                    }
                }
            };
        }

        public static int GetInputStructSize()
        {
            return Marshal.SizeOf(typeof(INPUT));
        }

        public void Dispose()
        {
            Stop();
        }

        #region Win32 P/Invoke

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWKEYBOARD
        {
            public ushort MakeCode;
            public ushort Flags;
            public ushort Reserved;
            public ushort VKey;
            public uint Message;
            public uint ExtraInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWinEventHook(
            uint eventMin,
            uint eventMax,
            IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc,
            uint idProcess,
            uint idThread,
            uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern uint RegisterWindowMessage(string lpString);

        #endregion
    }
}
