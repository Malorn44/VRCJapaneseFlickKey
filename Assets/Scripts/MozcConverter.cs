using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using MozcFfi;

// Kana-kanji conversion with Mozc, in process and offline (mozc-ffi: Assets/Plugins/MozcFfi).
// Mozc opens on a worker thread when the app starts, and every call runs there too, one at a time,
// so the overlay never waits on it. What the user picks is learned and ranks first next time; it is
// kept in the app's data folder (Application.persistentDataPath/mozc).
public static class MozcConverter
{
    private static readonly object gate = new object();
    private static Task<Mozc> opening;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Open()
    {
        var profile = Path.Combine(Application.persistentDataPath, "mozc");
        opening = Task.Run(() =>
        {
            try
            {
                Directory.CreateDirectory(profile);
                lock(gate) return new Mozc(profile);
            }
            catch(System.Exception e)
            {
                // Missing or broken mozc_ffi.dll: conversion stays off, typing still works.
                Debug.LogError($"Mozc failed to open: {e.Message}");
                return null;
            }
        });
        Application.quitting += Close;
    }

    // Saves what was learned.
    private static void Close()
    {
        var mozc = opening?.Result;
        if(mozc == null) return;
        lock(gate) mozc.Dispose();
    }

    // The reading's segments, each with its candidates best first; null if Mozc isn't available.
    // `lengths` fixes the segments (one length each); without it Mozc segments the reading itself.
    public static async Task<List<(string reading, List<string> candidates)>> GetSegmentsAsync(string reading,
        int[] lengths = null)
    {
        var mozc = await opening;
        if(mozc == null || string.IsNullOrEmpty(reading)) return null;
        return await Task.Run(() =>
        {
            lock(gate)
            {
                return mozc.Convert(reading, lengths)
                    .Select(s => (s.Reading, s.Candidates.ToList()))
                    .ToList();
            }
        });
    }

    // The user picked `text` for the first `reading`: it ranks first for that reading next time.
    public static async void Learn(string reading, string text)
    {
        var mozc = await opening;
        if(mozc == null) return;
        await Task.Run(() =>
        {
            lock(gate) mozc.Learn(reading, new[] { reading.Length }, new[] { text });
        });
    }
}
