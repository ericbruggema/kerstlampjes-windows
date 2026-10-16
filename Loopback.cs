using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Kerstlampjes
{
    /// <summary>
    /// Volume van het geluid dat de computer zelf afspeelt (YouTube, Spotify, ...), voor het effect "Op de muziek".
    /// Er wordt niets opgenomen: Windows geeft alleen de meterstand van de afspeelapparaten. Alle actieve
    /// afspeelapparaten (luidsprekers, koptelefoon, monitor, bluetooth) worden gemeten, het luidste telt.
    /// </summary>
    public static class SystemAudio
    {
        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        class MMDeviceEnumeratorClass { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        }

        [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceCollection
        {
            [PreserveSig] int GetCount(out uint count);
            [PreserveSig] int Item(uint index, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
            [PreserveSig] int OpenPropertyStore(int access, out IntPtr store);
            [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
            [PreserveSig] int GetState(out int state);
        }

        [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioMeterInformation
        {
            [PreserveSig] int GetPeakValue(out float peak);
        }

        static readonly List<IAudioMeterInformation> meters = new List<IAudioMeterInformation>();
        static float envelope = 0.02f;
        static long lastAcquireMs = -100000;
        static readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

        public static bool Running { get; private set; }
        /// <summary>Genormaliseerd volume, 0..1 (past zich aan het geluid aan).</summary>
        public static float Level;
        /// <summary>Ruwe meterstand van Windows (voor diagnose).</summary>
        public static float Peak;
        public static string Status = "";

        static IAudioMeterInformation MeterOf(IMMDevice device)
        {
            Guid iid = typeof(IAudioMeterInformation).GUID;
            object obj;
            if (device.Activate(ref iid, 23, IntPtr.Zero, out obj) != 0 || obj == null) return null; // CLSCTX_ALL
            return (IAudioMeterInformation)obj;
        }

        /// <summary>Zoekt alle actieve afspeelapparaten opnieuw (ze kunnen wisselen: koptelefoon, bluetooth, monitor).</summary>
        static void Acquire()
        {
            Release();
            lastAcquireMs = clock.ElapsedMilliseconds;
            try
            {
                var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorClass();
                IMMDeviceCollection devices;
                if (enumerator.EnumAudioEndpoints(0, 1, out devices) == 0 && devices != null) // eRender, DEVICE_STATE_ACTIVE
                {
                    uint n; devices.GetCount(out n);
                    for (uint i = 0; i < n; i++)
                    {
                        IMMDevice dev;
                        if (devices.Item(i, out dev) != 0 || dev == null) continue;
                        var m = MeterOf(dev);
                        if (m != null) meters.Add(m);
                    }
                }
                if (meters.Count == 0)
                {   // terugval: alleen het standaardapparaat
                    IMMDevice def;
                    if (enumerator.GetDefaultAudioEndpoint(0, 1, out def) == 0 && def != null)
                    {
                        var m = MeterOf(def);
                        if (m != null) meters.Add(m);
                    }
                }
            }
            catch { }
        }

        static void Release()
        {
            foreach (var m in meters) { try { Marshal.ReleaseComObject(m); } catch { } }
            meters.Clear();
        }

        public static bool Start()
        {
            if (Running) return true;
            Running = true; Level = 0f; envelope = 0.02f;
            Acquire();
            return true;
        }

        public static void Stop()
        {
            Running = false; Level = 0f;
            Release();
        }

        static float MaxPeak()
        {
            float best = 0f;
            for (int i = meters.Count - 1; i >= 0; i--)
            {
                float v = 0f;
                try { if (meters[i].GetPeakValue(out v) != 0) { v = 0f; lastAcquireMs = -100000; } }
                catch { v = 0f; lastAcquireMs = -100000; }
                if (v > best) best = v;
            }
            return best;
        }

        /// <summary>Leest de meterstanden en werkt Level bij; elk beeld aanroepen.</summary>
        public static void Poll()
        {
            if (!Running) return;
            if (meters.Count == 0 || clock.ElapsedMilliseconds - lastAcquireMs > 4000) Acquire();
            float v = MaxPeak();
            Peak = v; Status = meters.Count + " apparaat/apparaten";
            envelope = Math.Max(Math.Max(v, 0.004f), envelope * 0.997f);
            float lv = Math.Max(0f, Math.Min(1f, (v - 0.0008f) / (envelope - 0.0008f)));
            Level = Math.Max(lv, Level * 0.8f);
        }

        /// <summary>Diagnose: elk afspeelapparaat met zijn hoogste meterstand over ongeveer twee seconden.</summary>
        public static string Diagnose()
        {
            var sb = new StringBuilder();
            try
            {
                var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorClass();
                string defId = "?";
                IMMDevice def;
                if (enumerator.GetDefaultAudioEndpoint(0, 1, out def) == 0 && def != null) def.GetId(out defId);
                sb.AppendLine("standaard (multimedia): " + defId);
                IMMDevice defComm;
                string commId = "?";
                if (enumerator.GetDefaultAudioEndpoint(0, 0, out defComm) == 0 && defComm != null) defComm.GetId(out commId);
                sb.AppendLine("standaard (console): " + commId);
                IMMDeviceCollection devices;
                enumerator.EnumAudioEndpoints(0, 0xF, out devices); // alle statussen
                uint n; devices.GetCount(out n);
                var list = new List<KeyValuePair<string, IAudioMeterInformation>>();
                for (uint i = 0; i < n; i++)
                {
                    IMMDevice dev; devices.Item(i, out dev);
                    string id; dev.GetId(out id);
                    int state; dev.GetState(out state);
                    sb.AppendLine("apparaat " + i + ": " + id + " status=" + state + (id == defId ? " [standaard]" : ""));
                    if (state == 1) list.Add(new KeyValuePair<string, IAudioMeterInformation>(id, MeterOf(dev)));
                }
                var max = new float[list.Count];
                var t0 = System.Diagnostics.Stopwatch.StartNew();
                while (t0.ElapsedMilliseconds < 2000)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        float v = 0f;
                        if (list[i].Value != null) list[i].Value.GetPeakValue(out v);
                        if (v > max[i]) max[i] = v;
                    }
                    System.Threading.Thread.Sleep(30);
                }
                for (int i = 0; i < list.Count; i++) sb.AppendLine("meter " + list[i].Key + ": max " + max[i].ToString("0.0000"));
            }
            catch (Exception e) { sb.AppendLine("fout: " + e.Message); }
            return sb.ToString();
        }
    }

    /// <summary>Het niveau voor het effect "Op de muziek", uit de gekozen bron: 0 = computer, 1 = microfoon, 2 = beide.</summary>
    public static class MusicInput
    {
        public static float Get(int source)
        {
            if (source == 1) return MicLevel.Level;
            if (source == 2) return Math.Max(MicLevel.Level, SystemAudio.Level);
            return SystemAudio.Level;
        }
    }
}
