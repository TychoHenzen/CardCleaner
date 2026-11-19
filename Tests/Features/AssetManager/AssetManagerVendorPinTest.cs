using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Godot;

namespace CardCleaner.Tests.Features.AssetManager;

/// <summary>
/// <c>addons/asset_manager</c> is a copy of one commit of our fork of the Asset Manager, written by
/// <c>tools/asset-manager/sync.mjs</c>, and <c>tools/asset-manager/pin.json</c> records that commit and a hash of every
/// file. Every change to the addon is made in the fork and synced, so an upstream release merged there keeps our edits.
/// These cases fail when the copy here differs from the pin: a file edited, removed or added in place, or a store
/// download copied over the folder.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AssetManagerVendorPinTest
{
    private const string Addon = "res://addons/asset_manager";
    private const string Pin = "res://tools/asset-manager/pin.json";
    private const string HowToChange =
        "Make the change in the fork, merge it there, then run node tools/asset-manager/sync.mjs";

    [TestCase]
    public static void ThePinNamesOneForkCommit()
    {
        using var pin = ReadPin();
        var root = pin.RootElement;
        AssertThat(root.GetProperty("repository").GetString()).IsNotEmpty();
        AssertThat(Regex.IsMatch(root.GetProperty("commit").GetString() ?? string.Empty, "^[0-9a-f]{40}$")).IsTrue();
        AssertThat(root.GetProperty("path").GetString()).IsEqual("addons/asset_manager");
        AssertThat(PinnedHashes(pin).Count).IsGreater(0);
    }

    [TestCase]
    public static void EveryPinnedFileMatchesTheForkCommit()
    {
        using var pin = ReadPin();
        var addon = ProjectSettings.GlobalizePath(Addon);
        var differing = PinnedHashes(pin)
            .Where(entry =>
            {
                var file = Path.Combine(addon, entry.Key);
                return !File.Exists(file) || ContentHash(File.ReadAllBytes(file)) != entry.Value;
            })
            .Select(entry => entry.Key)
            .ToArray();
        var message = $"Missing or changed since the pinned commit: {string.Join(", ", differing)}. {HowToChange}";
        AssertThat(differing.Length).OverrideFailureMessage(message).IsEqual(0);
    }

    /// <summary>Godot writes a <c>.import</c> file beside each image it imports, and this repository ignores those.</summary>
    [TestCase]
    public static void TheAddonHoldsNoFileThePinLacks()
    {
        using var pin = ReadPin();
        var pinned = PinnedHashes(pin);
        var addon = ProjectSettings.GlobalizePath(Addon);
        var extra = Directory.EnumerateFiles(addon, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(addon, file).Replace('\\', '/'))
            .Where(path => !pinned.ContainsKey(path) && !path.EndsWith(".import", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var message = $"Not in the pinned commit: {string.Join(", ", extra)}. {HowToChange}";
        AssertThat(extra.Length).OverrideFailureMessage(message).IsEqual(0);
    }

    /// <summary>SHA-256 of a file, with CRLF read as LF in a file holding no NUL byte, as <c>sync.mjs</c> hashes.</summary>
    private static string ContentHash(byte[] bytes)
    {
        var data = bytes;
        if (Array.IndexOf(bytes, (byte)0) < 0)
        {
            var kept = new List<byte>(bytes.Length);
            for (var i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] == '\r' && i + 1 < bytes.Length && bytes[i + 1] == '\n')
                {
                    continue;
                }

                kept.Add(bytes[i]);
            }

            data = kept.ToArray();
        }

        return Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    }

    private static JsonDocument ReadPin() => JsonDocument.Parse(File.ReadAllText(ProjectSettings.GlobalizePath(Pin)));

    private static Dictionary<string, string> PinnedHashes(JsonDocument pin) =>
        pin.RootElement.GetProperty("files").EnumerateObject()
            .ToDictionary(file => file.Name, file => file.Value.GetString() ?? string.Empty, StringComparer.Ordinal);
}
