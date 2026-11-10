// C# bindings for mozc-ffi: Mozc's kana-kanji converter, in process.
// netstandard2.0 and C# 9, so it also works in Unity (Mono and IL2CPP).
// The native library is mozc_ffi.dll on Windows and libmozc_ffi.so on Linux.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace MozcFfi
{
    /// <summary>A segment of the reading and its candidates, best first.</summary>
    public sealed class Segment
    {
        public Segment(string reading, IReadOnlyList<string> candidates)
        {
            Reading = reading;
            Candidates = candidates;
        }

        public string Reading { get; }
        public IReadOnlyList<string> Candidates { get; }
    }

    public sealed class MozcException : Exception
    {
        public MozcException(string message) : base(message) { }
    }

    /// <summary>
    /// One Mozc engine. Every call is self-contained. Segment lengths count Unicode characters.
    /// Open one profile per process: Mozc keeps the profile directory globally.
    /// </summary>
    public sealed class Mozc : IDisposable
    {
        public const int AbiVersion = 1;
        const int Incognito = 1;

        readonly Native.Handle handle;

        /// <param name="profileDir">Where Mozc keeps what it learns; null for Mozc's default.</param>
        /// <param name="incognito">Learn nothing and leave the profile untouched.</param>
        public Mozc(string profileDir = null, bool incognito = false)
        {
            int abi = Native.mozc_ffi_abi_version();
            if (abi != AbiVersion)
                throw new MozcException($"mozc_ffi has ABI {abi}, these bindings need {AbiVersion}");
            handle = Native.mozc_ffi_open(Utf8(profileDir), incognito ? Incognito : 0);
            if (handle.IsInvalid)
                throw new MozcException("mozc_ffi_open failed");
        }

        public static string MozcVersion => FromUtf8(Native.mozc_ffi_mozc_version());

        /// <summary>Segments of the reading; empty if it can't be converted as asked.</summary>
        /// <param name="lengths">Fixes the segments: one length per segment, summing to the reading's length.</param>
        /// <param name="context">The text just before the reading.</param>
        public IReadOnlyList<Segment> Convert(string reading, IReadOnlyList<int> lengths = null, string context = null)
        {
            int[] sizes = ToArray(lengths);
            return Read(Native.mozc_ffi_convert(handle, Utf8(reading), sizes, sizes.Length, Utf8(context)));
        }

        /// <summary>Completions of the reading typed so far.</summary>
        public IReadOnlyList<string> Predict(string reading, string context = null)
        {
            var segments = Read(Native.mozc_ffi_predict(handle, Utf8(reading), Utf8(context)));
            return segments.Count > 0 ? segments[0].Candidates : Array.Empty<string>();
        }

        /// <summary>
        /// Learns a sent conversion: texts[i] picked for the segment of lengths[i], so the picks rank first
        /// next time. False, and nothing learned, if a text isn't a candidate of its segment.
        /// </summary>
        public bool Learn(string reading, IReadOnlyList<int> lengths, IReadOnlyList<string> texts, string context = null)
        {
            if (lengths == null || texts == null || texts.Count == 0 || lengths.Count != texts.Count)
                return false;
            var pointers = new IntPtr[texts.Count];
            try
            {
                for (int i = 0; i < texts.Count; i++)
                {
                    byte[] text = Utf8(texts[i]);
                    if (text == null)
                        return false;
                    pointers[i] = Marshal.AllocHGlobal(text.Length);
                    Marshal.Copy(text, 0, pointers[i], text.Length);
                }
                return Native.mozc_ffi_learn(handle, Utf8(reading), ToArray(lengths), pointers, texts.Count,
                                             Utf8(context)) == 1;
            }
            finally
            {
                foreach (IntPtr pointer in pointers)
                    if (pointer != IntPtr.Zero)
                        Marshal.FreeHGlobal(pointer);
            }
        }

        /// <summary>Forgets everything learned.</summary>
        public bool ClearLearning() => Native.mozc_ffi_clear_learning(handle) == 1;

        /// <summary>Writes what was learned to the profile.</summary>
        public bool Flush() => Native.mozc_ffi_flush(handle) == 1;

        /// <summary>Saves what was learned and frees the engine.</summary>
        public void Dispose() => handle.Dispose();

        static int[] ToArray(IReadOnlyList<int> values)
        {
            var array = new int[values?.Count ?? 0];
            for (int i = 0; i < array.Length; i++)
                array[i] = values[i];
            return array;
        }

        static byte[] Utf8(string text)
        {
            if (text == null)
                return null;
            var bytes = new byte[Encoding.UTF8.GetByteCount(text) + 1];  // ends in NUL
            Encoding.UTF8.GetBytes(text, 0, text.Length, bytes, 0);
            return bytes;
        }

        static string FromUtf8(IntPtr text)
        {
            if (text == IntPtr.Zero)
                return null;
            int length = 0;
            while (Marshal.ReadByte(text, length) != 0)
                length++;
            var bytes = new byte[length];
            Marshal.Copy(text, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }

        static IReadOnlyList<Segment> Read(IntPtr result)
        {
            if (result == IntPtr.Zero)
                return Array.Empty<Segment>();
            try
            {
                var segments = new Segment[Native.mozc_ffi_segment_count(result)];
                for (int s = 0; s < segments.Length; s++)
                {
                    var candidates = new string[Native.mozc_ffi_candidate_count(result, s)];
                    for (int c = 0; c < candidates.Length; c++)
                        candidates[c] = FromUtf8(Native.mozc_ffi_candidate(result, s, c));
                    segments[s] = new Segment(FromUtf8(Native.mozc_ffi_segment_reading(result, s)), candidates);
                }
                return segments;
            }
            finally
            {
                Native.mozc_ffi_result_free(result);
            }
        }

        static class Native
        {
            const string Library = "mozc_ffi";

            /// <summary>The engine; closed once no call is using it.</summary>
            internal sealed class Handle : SafeHandle
            {
                public Handle() : base(IntPtr.Zero, true) { }  // P/Invoke creates it
                public override bool IsInvalid => handle == IntPtr.Zero;
                protected override bool ReleaseHandle()
                {
                    mozc_ffi_close(handle);
                    return true;
                }
            }

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int mozc_ffi_abi_version();

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr mozc_ffi_mozc_version();

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern Handle mozc_ffi_open(byte[] profileDir, int flags);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void mozc_ffi_close(IntPtr mozc);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr mozc_ffi_convert(Handle mozc, byte[] reading, int[] lengths, int count,
                                                           byte[] context);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr mozc_ffi_predict(Handle mozc, byte[] reading, byte[] context);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int mozc_ffi_segment_count(IntPtr result);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr mozc_ffi_segment_reading(IntPtr result, int segment);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int mozc_ffi_candidate_count(IntPtr result, int segment);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr mozc_ffi_candidate(IntPtr result, int segment, int index);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void mozc_ffi_result_free(IntPtr result);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int mozc_ffi_learn(Handle mozc, byte[] reading, int[] lengths, IntPtr[] texts,
                                                      int count, byte[] context);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int mozc_ffi_clear_learning(Handle mozc);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int mozc_ffi_flush(Handle mozc);
        }
    }
}
