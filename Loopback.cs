using System;
using System.Runtime.InteropServices;

namespace Kerstlampjes
{
    /// <summary>
    /// Volume van het geluid dat de computer zelf afspeelt (YouTube, Spotify, ...), voor het effect "Op de muziek".
    /// Er wordt niets opgenomen: Windows geeft alleen de meterstand van het afspeelapparaat.
    /// </summary>
    public static class SystemAudio
    {
        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        class MMDeviceEnumeratorClass { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        }

        [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioMeterInformation
        {
            [PreserveSig] int GetPeakValue(out float peak);
        }

        static IAudioMeterInformation meter;
        static float envelope = 0.2f;
        static long lastAcquireMs;
        static readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

        public static bool Running { get; private set; }
        /// <summary>Genormaliseerd volume, 0..1 (past zich aan het geluid aan).</summary>
        public static float Level;
        /// <summary>Ruwe meterstand van Windows (voor diagnose).</summary>
        public static float Peak;
        public static string Status = "";

        static bool Acquire()
        {
            Release();
            lastAcquireMs = clock.ElapsedMilliseconds;
            try
            {
                var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorClass();
                IMMDevice device;
                if (enumerator.GetDefaultAudioEndpoint(0, 1, out device) != 0 || device == null) return false; // eRender, eMultimedia
                Guid iid = typeof(IAudioMeterInformation).GUID;
                object obj;
                if (device.Activate(ref iid, 23, IntPtr.Zero, out obj) != 0 || obj == null) return false; // CLSCTX_ALL
                meter = (IAudioMeterInformation)obj;
                return true;
            }
            catch { meter = null; return false; }
        }

        static void Release()
        {
            if (meter != null) { try { Marshal.ReleaseComObject(meter); } catch { } meter = null; }
        }

        public static bool Start()
        {
            if (Running) return true;
            Running = true; Level = 0f; envelope = 0.2f;
            Acquire();
            return true;
        }

        public static void Stop()
        {
            Running = false; Level = 0f;
            Release();
        }

        /// <summary>Leest de meterstand en werkt Level bij; elk beeld aanroepen.</summary>
        public static void Poll()
        {
            if (!Running) return;
            // af en toe opnieuw zoeken: de standaard-luidspreker kan wisselen (koptelefoon, bluetooth)
            if (meter == null || clock.ElapsedMilliseconds - lastAcquireMs > 4000) Acquire();
            float v = 0f;
            if (meter != null)
            {
                try { if (meter.GetPeakValue(out v) != 0) { v = 0f; Release(); } }
                catch { v = 0f; Release(); }
            }
            Peak = v; Status = meter == null ? "geen apparaat" : "ok";
            envelope = Math.Max(Math.Max(v, 0.05f), envelope * 0.997f);
            float lv = Math.Max(0f, Math.Min(1f, (v - 0.01f) / (envelope - 0.01f)));
            Level = Math.Max(lv, Level * 0.8f);
        }
    }
}
