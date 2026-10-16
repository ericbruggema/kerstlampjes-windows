using System;
using System.Runtime.InteropServices;

namespace Kerstlampjes
{
    /// <summary>
    /// Geluidsniveau van de microfoon voor het effect "Op de muziek". Alleen aan als dat effect is gekozen;
    /// het geluid wordt niet opgeslagen of verstuurd, alleen het volume wordt gemeten.
    /// </summary>
    public static class MicLevel
    {
        [StructLayout(LayoutKind.Sequential)]
        struct WAVEFORMATEX { public ushort wFormatTag, nChannels; public uint nSamplesPerSec, nAvgBytesPerSec; public ushort nBlockAlign, wBitsPerSample, cbSize; }

        [StructLayout(LayoutKind.Sequential)]
        struct WAVEHDR { public IntPtr lpData; public uint dwBufferLength, dwBytesRecorded; public IntPtr dwUser; public uint dwFlags, dwLoops; public IntPtr lpNext, reserved; }

        [DllImport("winmm.dll")] static extern int waveInOpen(out IntPtr h, uint device, ref WAVEFORMATEX fmt, IntPtr cb, IntPtr inst, uint flags);
        [DllImport("winmm.dll")] static extern int waveInClose(IntPtr h);
        [DllImport("winmm.dll")] static extern int waveInPrepareHeader(IntPtr h, IntPtr hdr, uint size);
        [DllImport("winmm.dll")] static extern int waveInUnprepareHeader(IntPtr h, IntPtr hdr, uint size);
        [DllImport("winmm.dll")] static extern int waveInAddBuffer(IntPtr h, IntPtr hdr, uint size);
        [DllImport("winmm.dll")] static extern int waveInStart(IntPtr h);
        [DllImport("winmm.dll")] static extern int waveInStop(IntPtr h);
        [DllImport("winmm.dll")] static extern int waveInReset(IntPtr h);

        const int Buffers = 3, BufferBytes = 800; // 50 ms bij 8 kHz, 16 bit, mono
        static readonly uint HdrSize = (uint)Marshal.SizeOf(typeof(WAVEHDR));
        static IntPtr wave;
        static readonly IntPtr[] hdrs = new IntPtr[Buffers];
        static float peak = 0.05f;

        public static bool Running { get; private set; }
        /// <summary>Genormaliseerd volume, 0..1 (past zich aan het geluid in de kamer aan).</summary>
        public static float Level;

        public static bool Start()
        {
            if (Running) return true;
            var fmt = new WAVEFORMATEX { wFormatTag = 1, nChannels = 1, nSamplesPerSec = 8000, nAvgBytesPerSec = 16000, nBlockAlign = 2, wBitsPerSample = 16, cbSize = 0 };
            if (waveInOpen(out wave, 0xFFFFFFFF, ref fmt, IntPtr.Zero, IntPtr.Zero, 0) != 0) { wave = IntPtr.Zero; return false; }
            for (int i = 0; i < Buffers; i++)
            {
                var h = new WAVEHDR { lpData = Marshal.AllocHGlobal(BufferBytes), dwBufferLength = BufferBytes };
                hdrs[i] = Marshal.AllocHGlobal((int)HdrSize);
                Marshal.StructureToPtr(h, hdrs[i], false);
                waveInPrepareHeader(wave, hdrs[i], HdrSize);
                waveInAddBuffer(wave, hdrs[i], HdrSize);
            }
            if (waveInStart(wave) != 0) { Stop(); return false; }
            Running = true;
            Level = 0f; peak = 0.05f;
            return true;
        }

        public static void Stop()
        {
            if (wave != IntPtr.Zero)
            {
                waveInStop(wave); waveInReset(wave);
                for (int i = 0; i < Buffers; i++)
                {
                    if (hdrs[i] == IntPtr.Zero) continue;
                    var h = (WAVEHDR)Marshal.PtrToStructure(hdrs[i], typeof(WAVEHDR));
                    waveInUnprepareHeader(wave, hdrs[i], HdrSize);
                    Marshal.FreeHGlobal(h.lpData);
                    Marshal.FreeHGlobal(hdrs[i]);
                    hdrs[i] = IntPtr.Zero;
                }
                waveInClose(wave);
                wave = IntPtr.Zero;
            }
            Running = false;
            Level = 0f;
        }

        /// <summary>Leest de volle buffers uit en werkt Level bij; vaak genoeg aanroepen (elk beeld).</summary>
        public static void Poll()
        {
            if (!Running) return;
            float decayed = Level * 0.8f;
            float best = decayed;
            for (int i = 0; i < Buffers; i++)
            {
                var h = (WAVEHDR)Marshal.PtrToStructure(hdrs[i], typeof(WAVEHDR));
                if ((h.dwFlags & 1) == 0 || h.dwBytesRecorded == 0) continue; // WHDR_DONE
                int n = (int)h.dwBytesRecorded / 2;
                double sum = 0;
                for (int k = 0; k < n; k++)
                {
                    double v = Marshal.ReadInt16(h.lpData, k * 2) / 32768.0;
                    sum += v * v;
                }
                float rms = n > 0 ? (float)Math.Sqrt(sum / n) : 0f;
                peak = Math.Max(Math.Max(rms, 0.02f), peak * 0.998f);
                float lv = Math.Max(0f, Math.Min(1f, (rms - 0.004f) / (peak - 0.004f)));
                if (lv > best) best = lv;
                h.dwBytesRecorded = 0; h.dwFlags &= ~1u;
                Marshal.StructureToPtr(h, hdrs[i], false);
                waveInAddBuffer(wave, hdrs[i], HdrSize);
            }
            Level = best;
        }
    }
}
