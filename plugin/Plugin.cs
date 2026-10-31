using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Socotra.UI;
using Steezy.Localize;

namespace TokyoRequiemEN
{
    [BepInPlugin(Guid, "Tokyo Requiem English", Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "tokyorequiem.en";
        public const string Version = "0.1.0";

        internal static ManualLogSource Log;
        internal static string Dir;

        // Optional gameplay enhancements: BepInEx\config\tokyorequiem.en.cfg (created on first run; read at startup).
        internal static BepInEx.Configuration.ConfigEntry<bool> ShopDescriptions, ReviveKeepsMp, DemonCompendium, CompendiumCostScales, NoSpellFlash, NoSummonAlignmentLimit, MinimapTrailOn;
        internal static BepInEx.Configuration.ConfigEntry<string> MinimapTrailColor;
        internal static BepInEx.Configuration.ConfigEntry<int> ExpRate, MoneyRate, MagRate;

        private void Awake()
        {
            Log = Logger;
            Dir = Path.GetDirectoryName(Info.Location);
            ShopDescriptions = Config.Bind("Enhancements", "ShopItemDescriptions", true,
                "Show a short description of the highlighted item above the shop's buy/sell list.");
            ReviveKeepsMp = Config.Bind("Enhancements", "ReviveKeepsMP", true,
                "Reviving an ally (Balm of Life, Jin Dan, revive spells) keeps their MP instead of setting it to 0.");
            var rate = new BepInEx.Configuration.AcceptableValueRange<int>(1, 100);
            ExpRate = Config.Bind("Enhancements", "ExpMultiplier", 1,
                new BepInEx.Configuration.ConfigDescription("Battle EXP multiplier (1 = original game).", rate));
            MoneyRate = Config.Bind("Enhancements", "MoneyMultiplier", 1,
                new BepInEx.Configuration.ConfigDescription("Battle money (yen) multiplier (1 = original game).", rate));
            MagRate = Config.Bind("Enhancements", "MagMultiplier", 1,
                new BepInEx.Configuration.ConfigDescription("Battle MAG multiplier (1 = original game).", rate));
            DemonCompendium = Config.Bind("Enhancements", "DemonCompendium", true,
                "Cathedral 'Compendium' records a party demon's level, stats and skills; when summoning that demon, " +
                "Left/Right switches between the base and the recorded version. Records are saved per save slot " +
                "(save\\compendium0-2).");
            CompendiumCostScales = Config.Bind("Enhancements", "CompendiumCostScalesWithLevel", true,
                "Summoning a recorded demon costs base cost x recorded level / base level (capped at 6350 MAG). " +
                "false = same cost as the base demon.");
            NoSpellFlash = Config.Bind("Enhancements", "NoSpellFlash", true,
                "Spells play the physical-attack animation (two bars closing in on the target) instead of the " +
                "full-screen white flash.");
            NoSummonAlignmentLimit = Config.Bind("Enhancements", "NoSummonAlignmentLimit", true,
                "Any demon can be summoned whatever its alignment: LAW and CHAOS demons can fight side by side, and " +
                "neither clashes with a LAW or CHAOS protagonist. The MAG requirement still applies.");
            MinimapTrailOn = Config.Bind("Enhancements", "MinimapTrail", true,
                "The minimap colours each tile the protagonist has walked on. Trails are saved per save slot " +
                "(save\\minitrail0-2) when the game saves.");
            MinimapTrailColor = Config.Bind("Enhancements", "MinimapTrailColor", "6084DC",
                "Colour of the minimap trail, as RRGGBB hex.");
            Log.LogInfo($"Enhancements: minimap trail {MinimapTrailOn.Value}, " +
                        $"no summon alignment limit {NoSummonAlignmentLimit.Value}, " +
                        $"no spell flash {NoSpellFlash.Value}, " +
                        $"shop descriptions {ShopDescriptions.Value}, revive keeps MP {ReviveKeepsMp.Value}, " +
                        $"EXP x{ExpRate.Value}, money x{MoneyRate.Value}, MAG x{MagRate.Value}, " +
                        $"compendium {DemonCompendium.Value} (cost scales {CompendiumCostScales.Value})");
            Strings.Load(Path.Combine(Dir, "strings"));
            Strings.LoadPatterns(Path.Combine(Dir, "strings"));
            Names.Load(Path.Combine(Dir, "strings"));
            IntroPatch.Load(Path.Combine(Dir, "strings"));
            // Patch class by class so one broken target (e.g. an ambiguous overload) can't disable the rest.
            var harmony = new Harmony(Guid);
            int ok = 0, failed = 0;
            foreach (var type in AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly))
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0) continue;
                try { harmony.CreateClassProcessor(type).Patch(); ok++; }
                catch (Exception e) { failed++; Log.LogError($"Patch {type.Name} failed: {e.InnerException?.Message ?? e.Message}"); }
            }
            Log.LogInfo($"Patches: {ok} applied, {failed} failed");
            Log.LogInfo($"Tokyo Requiem English {Version}: {Strings.Count} strings, {Strings.PatternCount} patterns, {Names.Summary} loaded");
            SelfTest(Path.Combine(Dir, "selftest.tsv"));
            WatchTables(Path.Combine(Dir, "strings"));
        }

        // Hot reload: copying new TSVs into the plugin folder takes effect without restarting the game.
        // The watcher fires on a worker thread; the reload itself runs in Update on the main thread.
        private FileSystemWatcher watcher;
        private volatile bool reloadPending;
        private float reloadAt;

        private void WatchTables(string dir)
        {
            if (!Directory.Exists(dir)) return;
            watcher = new FileSystemWatcher(dir, "*.tsv") { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size };
            FileSystemEventHandler onChange = (s, e) => reloadPending = true;
            watcher.Changed += onChange;
            watcher.Created += onChange;
            watcher.EnableRaisingEvents = true;
        }

        private void Update()
        {
            if (!reloadPending) return;
            if (reloadAt == 0) { reloadAt = UnityEngine.Time.realtimeSinceStartup + 1f; return; }   // let the copy finish
            if (UnityEngine.Time.realtimeSinceStartup < reloadAt) return;
            reloadPending = false;
            reloadAt = 0;
            try
            {
                var dir = Path.Combine(Dir, "strings");
                Strings.Load(dir);
                Strings.LoadPatterns(dir);
                Names.Load(dir);
                IntroPatch.Load(dir);
                Log.LogInfo($"Tables reloaded: {Strings.Count} strings, {Strings.PatternCount} patterns");
                SelfTest(Path.Combine(Dir, "selftest.tsv"));
            }
            catch (Exception e) { Log.LogError($"Reload failed: {e.Message}"); }
        }

        /// <summary>Regression check for the tables: each line is input TAB expected English.</summary>
        private static void SelfTest(string path)
        {
            if (!File.Exists(path)) return;
            int pass = 0, fail = 0;
            foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                if (raw.Length == 0 || raw[0] == '#') continue;
                var c = raw.Split('\t');
                if (c.Length < 2) continue;
                var got = Strings.TryGet(c[0], out var en) ? en : null;
                if (got == c[1]) pass++;
                else { fail++; Log.LogWarning($"SELFTEST FAIL: '{c[0]}' -> '{got ?? "(no match)"}', expected '{c[1]}'"); }
            }
            Log.LogInfo($"Self-test: {pass} passed, {fail} failed");
        }
    }

    /// <summary>JP -> EN exact-match table, loaded from every *.tsv in the strings folder.
    /// Format per line: japanese TAB english. "\n" is an escaped newline; lines starting with # are comments.</summary>
    internal static class Strings
    {
        private static readonly Dictionary<string, string> map = new Dictionary<string, string>();
        private static readonly HashSet<string> missing = new HashSet<string>();
        private static string missingPath;

        public static int Count => map.Count;

        public static void Load(string dir)
        {
            map.Clear();
            missingPath = Path.Combine(Plugin.Dir, "untranslated.txt");
            if (!Directory.Exists(dir))
            {
                Plugin.Log.LogWarning($"No strings folder at {dir}");
                return;
            }
            foreach (var file in Directory.GetFiles(dir, "*.tsv"))
            {
                var fileName = Path.GetFileName(file);
                if (fileName.StartsWith("names_") || fileName.StartsWith("patterns_") || fileName.StartsWith("pages_")) continue;
                int n = 0;
                foreach (var raw in File.ReadAllLines(file, Encoding.UTF8))
                {
                    n++;
                    if (raw.Length == 0 || raw[0] == '#') continue;
                    var cols = raw.Split('\t');
                    if (cols.Length < 2 || cols[1].Length == 0) continue;
                    var jp = Unescape(cols[0]);
                    if (map.ContainsKey(jp))
                        Plugin.Log.LogWarning($"{Path.GetFileName(file)}:{n} duplicate key {jp}");
                    map[jp] = Unescape(cols[1]);
                }
            }
        }

        /// <summary>"\n" / "\t" escapes; a value wrapped in double quotes keeps its exact leading/trailing spaces
        /// (editors and tools tend to strip trailing whitespace).</summary>
        private static string Unescape(string s)
        {
            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"') s = s.Substring(1, s.Length - 2);
            return s.Replace("\\n", "\n").Replace("\\t", "\t");
        }

        // patterns_*.tsv: anchored JP regex TAB EN template ({1}, {2}... = captured groups, each translated in turn).
        private static readonly List<KeyValuePair<Regex, string>> patterns = new List<KeyValuePair<Regex, string>>();
        private static readonly Dictionary<string, string> cache = new Dictionary<string, string>();
        private static readonly Regex groupRef = new Regex(@"\{(\d)\}");

        public static int PatternCount => patterns.Count;

        public static void LoadPatterns(string dir)
        {
            patterns.Clear();
            battlePatterns.Clear();
            cache.Clear();
            if (!Directory.Exists(dir)) return;
            foreach (var file in Directory.GetFiles(dir, "patterns_*.tsv"))
            {
                int n = 0;
                foreach (var raw in File.ReadAllLines(file, Encoding.UTF8))
                {
                    n++;
                    if (raw.Length == 0 || raw[0] == '#') continue;
                    var cols = raw.Split('\t');
                    if (cols.Length < 2 || cols[1].Length == 0) continue;
                    try
                    {
                        var re = new Regex("^" + cols[0] + "$", RegexOptions.Singleline);
                        patterns.Add(new KeyValuePair<Regex, string>(re, Unescape(cols[1])));
                        if (Path.GetFileName(file) == "patterns_battle.tsv") battlePatterns.Add(re);
                    }
                    catch (ArgumentException e)
                    {
                        Plugin.Log.LogWarning($"{Path.GetFileName(file)}:{n} bad pattern: {e.Message}");
                    }
                }
            }
        }

        /// <summary>Exact match first, then the first matching pattern. Cached (DrawString runs every frame).</summary>
        public static bool TryGet(string jp, out string en)
        {
            if (map.TryGetValue(jp, out en)) return true;
            if (patterns.Count == 0) return false;
            if (cache.TryGetValue(jp, out en)) return en != null;
            en = ApplyPatterns(jp, 0);
            if (cache.Count > 20000) cache.Clear();
            cache[jp] = en;
            return en != null;
        }

        private static string ApplyPatterns(string s, int depth)
        {
            foreach (var p in patterns)
            {
                var m = p.Key.Match(s);
                if (!m.Success) continue;
                var result = groupRef.Replace(p.Value, g =>
                {
                    int i = g.Groups[1].Value[0] - '0';
                    if (i >= m.Groups.Count) return g.Value;
                    var part = m.Groups[i].Value;
                    if (map.TryGetValue(part, out var t)) return t;
                    var sub = depth < 2 ? ApplyPatterns(part, depth + 1) : null;
                    return sub ?? TranslateWords(part);
                });
                if (depth == 0 && result.Length > BattleMaxChars && battlePatterns.Contains(p.Key))
                    result = ShortenNames(result);
                return result;
            }
            return null;
        }

        // Battle lines are drawn one by one without wrapping, about 37 half-width chars wide. When a battle template
        // overflows, swap long demon names for their "@narrow.{name}" short forms (the infirmary's, system.tsv) until
        // it fits, e.g. "Kushinada-Hime is afflicted with SHOCK!" -> "Kushinada is afflicted with SHOCK!".
        private const int BattleMaxChars = 37;
        private static readonly HashSet<Regex> battlePatterns = new HashSet<Regex>();

        private static string ShortenNames(string line)
        {
            foreach (var kv in map)
            {
                if (line.Length <= BattleMaxChars) break;
                if (!kv.Key.StartsWith("@narrow.")) continue;
                var name = kv.Key.Substring("@narrow.".Length);
                if (name.Length > kv.Value.Length && line.Contains(name)) line = line.Replace(name, kv.Value);
            }
            return line;
        }

        /// <summary>Captures like "屍鬼 Lady Zombie" (race + name, as in the encounter line): translate each
        /// space-separated word that has an exact entry, keep the rest.</summary>
        private static string TranslateWords(string part)
        {
            if (part.IndexOf(' ') < 0) return part;
            var words = part.Split(' ');
            for (int i = 0; i < words.Length; i++)
                if (map.TryGetValue(words[i], out var w)) words[i] = w;
            return string.Join(" ", words);
        }

        /// <summary>Record a Japanese string we drew without a translation (once per string).</summary>
        public static void NoteMissing(string s, string where)
        {
            if (!HasJapanese(s) || !missing.Add(s)) return;
            try
            {
                File.AppendAllText(missingPath, $"{where}\t{s.Replace("\n", "\\n")}\n", Encoding.UTF8);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not write {missingPath}: {e.Message}");
            }
        }

        public static bool HasJapanese(string s)
        {
            foreach (var c in s)
                if ((c >= '\u3040' && c <= '\u30ff') || (c >= '\u4e00' && c <= '\u9fff') || (c >= '\uff01' && c <= '\uff9f'))
                    return true;
            return false;
        }
    }

    /// <summary>ID-keyed name tables from names_demon.tsv / names_skill.tsv / names_item.tsv.
    /// Format per line: id TAB jp TAB en [TAB source TAB note]. The jp column guards against swapping a
    /// renamed character (protagonist, heroine) that happens to share an ID slot.</summary>
    internal static class Names
    {
        private static readonly Dictionary<string, Dictionary<int, KeyValuePair<string, string>>> kinds =
            new Dictionary<string, Dictionary<int, KeyValuePair<string, string>>>();

        public static string Summary
        {
            get
            {
                var parts = new List<string>();
                foreach (var k in kinds) parts.Add($"{k.Value.Count} {k.Key}");
                return parts.Count == 0 ? "no names" : string.Join(", ", parts.ToArray()) + " names";
            }
        }

        public static void Load(string dir)
        {
            kinds.Clear();
            if (!Directory.Exists(dir)) return;
            foreach (var file in Directory.GetFiles(dir, "names_*.tsv"))
            {
                var kind = Path.GetFileNameWithoutExtension(file).Substring("names_".Length);
                var table = new Dictionary<int, KeyValuePair<string, string>>();
                foreach (var raw in File.ReadAllLines(file, Encoding.UTF8))
                {
                    if (raw.Length == 0 || raw[0] == '#') continue;
                    var c = raw.Split('\t');
                    if (c.Length < 3 || c[2].Length == 0 || !int.TryParse(c[0], out var id)) continue;
                    table[id] = new KeyValuePair<string, string>(c[1], c[2]);
                }
                kinds[kind] = table;
            }
        }

        /// <summary>English name for (kind, id) if the current Japanese text is the stock name for that ID
        /// (or a truncated English name read back from a save).</summary>
        public static bool TryGet(string kind, int id, string current, out string en)
        {
            en = null;
            if (!kinds.TryGetValue(kind, out var table) || !table.TryGetValue(id, out var pair)) return false;
            if (current != null && current != pair.Key && !(current.Length > 0 && pair.Value.StartsWith(current))) return false;
            en = pair.Value;
            return true;
        }
    }

    /// <summary>Demon names: from the data record (bin[0] = id, bin[1..16] = SJIS name)...</summary>
    [HarmonyPatch(typeof(CCharacter), nameof(CCharacter.Init_SetName))]
    internal static class DemonNamePatch
    {
        private static void Postfix(CCharacter __instance, sbyte[] bin)
        {
            if (bin != null && bin.Length > 0 && Names.TryGet("demon", bin[0] & 0xFF, __instance.Name, out var en))
                __instance.Name = en;
        }
    }

    /// <summary>...and from save data, which stores the name text itself (Id is read after it).</summary>
    [HarmonyPatch(typeof(CCharacter), nameof(CCharacter.SetLoadData))]
    internal static class DemonNameLoadPatch
    {
        private static void Postfix(CCharacter __instance)
        {
            if (__instance.Id > 0 && __instance.Id < 252 && Names.TryGet("demon", __instance.Id, __instance.Name, out var en))
                __instance.Name = en;
        }
    }

    [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.Skill_GetName))]
    internal static class SkillNamePatch
    {
        private static void Postfix(sbyte[] bin, ref string __result)
        {
            if (bin != null && bin.Length > 0 && Names.TryGet("skill", bin[0] & 0xFF, __result, out var en)) __result = en;
        }
    }

    [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.Item_GetName))]
    internal static class ItemNamePatch
    {
        private static void Postfix(sbyte[] bin, ref string __result)
        {
            if (bin != null && bin.Length > 0 && Names.TryGet("item", bin[0] & 0xFF, __result, out var en)) __result = en;
        }
    }

    /// <summary>G-MODE's wrapper ships an English column; the game just hardcodes "ja".</summary>
    [HarmonyPatch(typeof(LocalizeSettings), nameof(LocalizeSettings.SetLocalizeLanguage))]
    internal static class LanguagePatch
    {
        private static bool Prefix()
        {
            Localization.language = "en";
            return false;
        }
    }

    /// <summary>While a Steam Input pad owns P1 the game ignores the keyboard; let every device drive P1.</summary>
    [HarmonyPatch(typeof(SteamPadManager), nameof(SteamPadManager.UpdateProcess))]
    internal static class AllDevicesPatch
    {
        private static void Prefix(SteamPadManager __instance) => __instance.isInputAllDevice = true;
    }

    /// <summary>isInputAllDevice alone wasn't enough: while a pad holds P1 the keyboard player count is 0, so
    /// GetButtonMapKeyboard falls into its multi-keyboard branch, finds no keyboard index for P1 and returns an empty
    /// key list. Give P1 the normal single-keyboard map in that case, so keyboard and pad really work together.</summary>
    [HarmonyPatch(typeof(SteamPadManager), "GetButtonMapKeyboard")]
    internal static class KeyboardMapForPadPlayer
    {
        private static readonly AccessTools.FieldRef<SteamPadManager, Dictionary<StPadManager.PadButton, List<UnityEngine.KeyCode>>> Map =
            AccessTools.FieldRefAccess<SteamPadManager, Dictionary<StPadManager.PadButton, List<UnityEngine.KeyCode>>>("ButtonMapKeyboard");

        private static void Postfix(SteamPadManager __instance, StPadManager.PadButton button, StPadManager.Player targetPlayer,
            ref List<UnityEngine.KeyCode> __result)
        {
            if (__result != null && __result.Count > 0 || targetPlayer != StPadManager.Player.P1) return;
            var map = Map(__instance);
            if (map != null && map.TryGetValue(button, out var keys)) __result = keys;
        }
    }

    /// <summary>Every line of game text goes through here: swap exact matches, log the rest.</summary>
    [HarmonyPatch(typeof(StGraphics), nameof(StGraphics.DrawString))]
    internal static class DrawStringPatch
    {
        private static readonly AccessTools.FieldRef<StGraphics, StFont> CurrentFont =
            AccessTools.FieldRefAccess<StGraphics, StFont>("currentFont");
        private static bool drawingExtra;

        private static void Prefix(StGraphics __instance, ref string str, ref int x, int y)
        {
            if (string.IsNullOrEmpty(str) || drawingExtra) return;
            Translate(ref str, ref x, y);
            // A one-line DrawString spot (e.g. the Cathedral greeting) can't wrap: a "\n" in the English draws the
            // extra lines below, one font height + 1 apart (the spacing DrawComment uses).
            int nl = str.IndexOf('\n');
            if (nl < 0) return;
            var lines = str.Split('\n');
            str = lines[0];
            var font = CurrentFont(__instance);
            int step = (font != null ? font.GetHeight() : 12) + 1;
            drawingExtra = true;
            try
            {
                for (int i = 1; i < lines.Length; i++)
                    __instance.DrawString(lines[i], x, y + i * step);
            }
            finally { drawingExtra = false; }
        }

        private static void Translate(ref string str, ref int x, int y)
        {
            if (ColumnLayout.Key != null) x = ColumnLayout.Map(x, y);
            if (NarrowScreens.Active && Strings.TryGet("@narrow." + str, out var shortEn))
                str = shortEn;
            else if (FusionGreeting.TryGet(str, out var greet))
                str = greet;
            else if (Strings.TryGet(str, out var en))
            {
                if (en != str && Strings.HasJapanese(en)) Strings.NoteMissing(str, "DrawString(partial)");
                str = en;
            }
            else
                Strings.NoteMissing(str, "DrawString");
        }
    }

    /// <summary>Screens with fixed columns sized for the original text (e.g. the infirmary's 6-letter status column).
    /// While one of them draws, DrawString prefers an "@narrow.{source}" entry (PALYZE -> PARLYZ instead of PARALYZE).
    /// Add more draw methods to TargetMethods as tight spots turn up.</summary>
    [HarmonyPatch]
    internal static class NarrowScreens
    {
        public static bool Active;

        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(MegatenCanvas), nameof(MegatenCanvas.HealingRoom_BasicDraw));
            yield return AccessTools.Method(typeof(MegatenCanvas), nameof(MegatenCanvas.HeailngRoom_HealingDraw));
        }

        private static void Prefix() => Active = true;
        private static void Finalizer() => Active = false;
    }

    /// <summary>The infirmary cuts demon names to 6 characters (StString.Substring(name, 0, 6)): right for 6 full-width
    /// kana, but "Forneus" became "Forneu". The name column (x 14 to the "/" at 85) holds 11 half-width letters, so
    /// reroute that call: English names up to 11 letters stay whole; longer ones get "@narrow.{name}" (system.tsv,
    /// swapped in by DrawString since these are NarrowScreens), else a hard cut at 11. Japanese keeps the original cut.
    /// A transpiler, because the tiny StString.Substring itself may be inlined by the JIT.</summary>
    [HarmonyPatch]
    internal static class InfirmaryNameFit
    {
        private const int MaxLetters = 11;
        private static readonly System.Reflection.MethodInfo Original =
            AccessTools.Method(AccessTools.TypeByName("Socotra.StString"), "Substring", new[] { typeof(string), typeof(int), typeof(int) });

        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(MegatenCanvas), nameof(MegatenCanvas.HealingRoom_BasicDraw));
            yield return AccessTools.Method(typeof(MegatenCanvas), nameof(MegatenCanvas.HeailngRoom_HealingDraw));
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
        {
            var fit = AccessTools.Method(typeof(InfirmaryNameFit), nameof(Fit));
            foreach (var ins in code)
            {
                if (Original != null && ins.Calls(Original)) ins.operand = fit;
                yield return ins;
            }
        }

        public static string Fit(string str, int index, int endIndex)
        {
            if (string.IsNullOrEmpty(str) || Strings.HasJapanese(str)) return str.Substring(index, endIndex - index);
            if (str.Length <= MaxLetters || Strings.TryGet("@narrow." + str, out _)) return str;
            return str.Substring(0, MaxLetters);
        }
    }

    /// <summary>Screens that draw columns at fixed x sized for Japanese. While one is active, DrawString moves any text
    /// drawn exactly at a listed x (and at y >= MinY) to a new x. "@layout.{key}" in system.tsv (hot-reloads) holds
    /// "from:to" pairs, comma-separated.</summary>
    internal static class ColumnLayout
    {
        public static string Key;
        public static int MinY;

        public static int Map(int x, int y)
        {
            if (Key == null || y < MinY || !Strings.TryGet("@layout." + Key, out var s)) return x;
            foreach (var pair in s.Split(','))
            {
                var p = pair.Split(':');
                if (p.Length == 2 && int.TryParse(p[0].Trim(), out var from) && from == x && int.TryParse(p[1].Trim(), out var to))
                    return to;
            }
            return x;
        }
    }

    /// <summary>The first-demon list (EventDraw, GenWorkShort[2] == 23): the race column is only 30 px wide, too narrow
    /// for "Therian", while the name column has room to spare. Layout key "firstdemon".</summary>
    [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.EventDraw))]
    internal static class FirstDemonColumns
    {
        private static void Prefix(MegatenCanvas __instance)
        {
            if (__instance.GenWorkShort[2] == 23) { ColumnLayout.Key = "firstdemon"; ColumnLayout.MinY = 0; }
        }
        private static void Finalizer() => ColumnLayout.Key = null;
    }

    /// <summary>Equip screen (EquipSetDraw): the weapon/armor info box at the bottom (y >= 155) puts labels at 5/75/175
    /// and values at 55/110-135/205, so "Align" ran into its value. Layout key "equip".</summary>
    [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.EquipSetDraw))]
    internal static class EquipInfoColumns
    {
        private static void Prefix() { ColumnLayout.Key = "equip"; ColumnLayout.MinY = 155; }
        private static void Finalizer() => ColumnLayout.Key = null;
    }

    /// <summary>Enhancement: the shop never says what an item does. Draw a two-line box above the buy/sell list
    /// with "@desc.item.{id}" (item_desc.tsv) for the item under the cursor. Skipped in the quantity step
    /// (GenWorkByte[1] &amp; 0x10), whose TOTAL / count windows use the same area (y 45-75).</summary>
    [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.Shop_BuyAnsSellDraw))]
    internal static class ShopDescPatch
    {
        private const int BoxX = 10, BoxY = 41, BoxW = 221, BoxH = 38, Pad = 6, MaxLines = 2;

        private static string lastError;

        // The game swallows exceptions from its draw calls (and then skips the rest of the frame), so never throw.
        private static void Postfix(MegatenCanvas __instance)
        {
            try { Draw(__instance); }
            catch (Exception e)
            {
                if (e.ToString() == lastError) return;
                lastError = e.ToString();
                Plugin.Log.LogError($"ShopDescPatch: {e}");
            }
        }

        // Diagnostic only: report (but don't swallow) an exception from the original Shop_BuyAnsSellDraw.
        private static Exception Finalizer(Exception __exception)
        {
            if (__exception != null && __exception.ToString() != lastError)
            {
                lastError = __exception.ToString();
                Plugin.Log.LogError($"Shop_BuyAnsSellDraw threw: {__exception}");
            }
            return __exception;
        }

        private static void Draw(MegatenCanvas __instance)
        {
            if (!Plugin.ShopDescriptions.Value) return;
            int mode = __instance.GenWorkByte[1];
            if ((mode & 0x10) != 0) return;
            int index = __instance.GenWorkShort[3] + __instance.GenWorkByte[0];
            var ids = (mode & 1) != 0 ? __instance.shop_item_id : __instance.char_item_id;
            if (ids == null || index < 0 || index >= ids.Length || ids[index] <= 0) return;
            if (!Strings.TryGet("@desc.item." + ids[index], out var desc)) return;
            var g = __instance.g;
            var font = __instance.font;
            if (g == null || font == null) return;
            __instance.Iface_DrawGenWindow(BoxX, BoxY, BoxW, BoxH);
            g.SetColor(StGraphics.GetColorOfRGB(255, 255, 255));
            int height = font.GetHeight();
            var lines = Wrap.Apply(desc, BoxW - 2 * Pad, font).Split('\n');
            for (int i = 0; i < lines.Length && i < MaxLines; i++)
                g.DrawString(lines[i], BoxX + Pad, BoxY + 3 + i * (height + 1) + height);
        }
    }

    /// <summary>The Cathedral's "今後ともヨロシク・・・" is one fixed line for every fusion. Give the new demon
    /// (Party_Work[7]) the greeting of its negotiation voice instead: "@greet.fuse.{Battel_Talk_Type[id]}"
    /// (0 stiff, 1 gruff, 2 lordly, 3 polite, 4 girly, 5 kiddy). Voices without an entry
    /// (6 = can't talk) keep the plain translation.</summary>
    [HarmonyPatch]
    internal static class FusionGreeting
    {
        private const string Jp = "今後ともヨロシク・・・";
        private static MegatenCanvas canvas;
        private static readonly AccessTools.FieldRef<MegatenCanvas, sbyte[]> TalkType =
            AccessTools.FieldRefAccess<MegatenCanvas, sbyte[]>("Battel_Talk_Type");

        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(MegatenCanvas), nameof(MegatenCanvas.Union_UnionDraw));
            yield return AccessTools.Method(typeof(MegatenCanvas), nameof(MegatenCanvas.Union_SamonSetAfterDraw));
            yield return AccessTools.Method(typeof(MegatenCanvas), nameof(MegatenCanvas.Union_SamonActionDraw));
        }

        private static void Prefix(MegatenCanvas __instance) => canvas = __instance;
        private static void Finalizer() => canvas = null;

        public static bool TryGet(string str, out string en)
        {
            en = null;
            if (canvas == null || str != Jp) return false;
            var demon = canvas.Party_Work?[7];
            var types = TalkType(canvas);
            if (demon == null || types == null || demon.Id < 0 || demon.Id >= types.Length) return false;
            return Strings.TryGet("@greet.fuse." + types[demon.Id], out en);
        }
    }

    /// <summary>Windows are sized from the source strings (e.g. Iface_DrawCommandWindow measures StringArray),
    /// so measure the English text that will actually be drawn.</summary>
    [HarmonyPatch(typeof(StFont), nameof(StFont.StringWidth))]
    internal static class StringWidthPatch
    {
        private static void Prefix(ref string str)
        {
            if (!string.IsNullOrEmpty(str) && Strings.TryGet(str, out var en))
                str = en;
        }
    }

    /// <summary>Word wrap for English. The game breaks lines at any character once a line is too wide, which
    /// splits English words. We turn the space at each break point into '\n' (the game's own break char), so the
    /// string length never changes; typewriter counters and page offsets (GenWorkShort) stay valid.
    /// Width math mirrors StFont.StringWidth: half-width chars count size/2, everything else size.</summary>
    internal static class Wrap
    {
        public static string Apply(string s, int maxPx, StFont font)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf(' ') < 0 || font == null || maxPx <= 0) return s;
            float full = font.GetWidth(), half = full / 2f;
            var c = s.ToCharArray();
            int lastSpace = -1;
            float width = 0, widthAtSpace = 0;
            for (int i = 0; i < c.Length; i++)
            {
                // explicit breaks: real '\n' or the two-char escape "\n" used in event data
                if (c[i] == '\n' || (c[i] == '\\' && i + 1 < c.Length && c[i + 1] == 'n'))
                {
                    if (c[i] == '\\') i++;
                    width = 0; lastSpace = -1;
                    continue;
                }
                if (c[i] == ' ') { lastSpace = i; widthAtSpace = width; }
                width += IsHalf(c[i]) ? half : full;
                if (width > maxPx && lastSpace >= 0)
                {
                    c[lastSpace] = '\n';
                    width -= widthAtSpace + half;   // what's left after the old space
                    lastSpace = -1;
                }
            }
            return new string(c);
        }

        private static bool IsHalf(char ch) => ch < 0x80 || (ch >= '｡' && ch <= 'ﾟ');
    }

    /// <summary>DrawComment breaks only by width and ignores '\n', so for English we draw it ourselves:
    /// same colour, same line positions (_y + line * (height + 1) + height), breaks at spaces.</summary>
    [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.DrawComment))]
    internal static class DrawCommentWrapPatch
    {
        private static bool Prefix(MegatenCanvas __instance, string mes, int _x, int _y, int turnNum)
        {
            if (string.IsNullOrEmpty(mes)) return true;
            if (Strings.TryGet(mes, out var en)) mes = en;
            if (mes.IndexOf(' ') < 0) return true;           // Japanese: the original breaker is fine
            var g = __instance.g;
            var font = __instance.font;
            if (g == null || font == null) return true;
            g.SetColor(StGraphics.GetColorOfRGB(255, 255, 255));
            int height = font.GetHeight();
            var lines = Wrap.Apply(mes, turnNum - _x, font).Split('\n');
            for (int i = 0; i < lines.Length; i++)
                g.DrawString(lines[i], _x, _y + i * (height + 1) + height);
            return false;
        }
    }

    /// <summary>Negotiation messages (Talk_Draw): up to three StringArray lines (12-14) drawn without wrapping, and
    /// the game splits some sentences over two of them ("{name}は" + "満足して去っていった"). We look the lines up
    /// joined first, then one by one, wrap the English to the box and draw up to four lines (the window reserves
    /// four). Mode 3 (the talk menu and the Give prompt) keeps the original drawing.</summary>
    [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.Talk_Draw))]
    internal static class TalkDrawPatch
    {
        private const int TextX = 9, MaxPx = 222, MaxLines = 4;
        private static readonly AccessTools.FieldRef<MegatenCanvas, string[]> StringArray =
            AccessTools.FieldRefAccess<MegatenCanvas, string[]>("StringArray");

        private static bool Prefix(MegatenCanvas __instance)
        {
            int mode = __instance.GenWorkByte[13];
            int count = mode == 1 ? 1 : mode == 2 ? 2 : mode == 4 || mode == 5 ? 3 : 0;
            var sa = StringArray(__instance);
            var g = __instance.g;
            var font = __instance.font;
            if (count == 0 || sa == null || g == null || font == null) return true;
            var src = new List<string>();
            for (int i = 0; i < count; i++)
                if (!string.IsNullOrEmpty(sa[12 + i])) src.Add(sa[12 + i]);
            var sentences = Translate(src);
            if (sentences == null) return true;               // nothing we know: draw (and log) as usual
            var lines = new List<string>();
            foreach (var s in sentences) lines.AddRange(Wrap.Apply(s, MaxPx, font).Split('\n'));
            if (lines.Count > MaxLines)                       // too many short sentences: flow them as one paragraph
                lines = new List<string>(Wrap.Apply(string.Join(" ", sentences.ToArray()), MaxPx, font).Split('\n'));
            int[] win = __instance.Iface_DrawNaviWindow(4, -1, -1);
            if (mode == 4)
            {
                // same colours as the original: white, green (it liked that), red (it didn't)
                int mood = __instance.GenWorkByte[28];
                if (mood == 0) g.SetColor(StGraphics.GetColorOfRGB(255, 255, 255));
                if (mood == 1) g.SetColor(StGraphics.GetColorOfRGB(127, 255, 127));
                if (mood == -1) g.SetColor(StGraphics.GetColorOfRGB(255, 0, 0));
            }
            for (int i = 0; i < lines.Count && i < MaxLines; i++)
                g.DrawString(lines[i], TextX, win[0] + win[1] * i);
            return false;
        }

        /// <summary>Longest joined run of lines that has a translation wins; null if no line translates at all.</summary>
        private static List<string> Translate(List<string> src)
        {
            var result = new List<string>();
            bool any = false;
            for (int i = 0; i < src.Count;)
            {
                int j = src.Count;
                string en = null;
                for (; j > i; j--)
                    if (Strings.TryGet(string.Concat(src.GetRange(i, j - i).ToArray()), out en)) break;
                if (j > i) { result.Add(en); any = true; i = j; continue; }
                Strings.NoteMissing(src[i], "Talk_Draw");
                result.Add(src[i]);
                i++;
            }
            return any ? result : null;
        }
    }

    /// <summary>The New Game prologue is MegatenCanvas.intro: 21 pages of pre-broken lines, each centred and typed out
    /// (TitleProc / DrawTitle). English pages come from pages_intro.tsv (page TAB line TAB text); line counts may
    /// differ from the Japanese (max 11 per page). Pages missing from the table keep the Japanese.</summary>
    [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.TitleProc))]
    internal static class IntroPatch
    {
        private static string[][] english;

        public static void Load(string dir)
        {
            english = null;
            version++;
            var path = Path.Combine(dir, "pages_intro.tsv");
            if (!File.Exists(path)) return;
            var pages = new SortedDictionary<int, SortedDictionary<int, string>>();
            foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                if (raw.Length == 0 || raw[0] == '#') continue;
                var c = raw.Split('\t');
                if (c.Length < 2 || !int.TryParse(c[0], out var p) || !int.TryParse(c[1], out var n)) continue;
                if (!pages.TryGetValue(p, out var lines)) pages[p] = lines = new SortedDictionary<int, string>();
                lines[n] = c.Length > 2 ? c[2] : string.Empty;
            }
            if (pages.Count == 0) return;
            english = new string[pages.Keys.Max() + 1][];
            foreach (var kv in pages) english[kv.Key] = kv.Value.Values.ToArray();
            Plugin.Log.LogInfo($"Intro: {pages.Count} English pages");
        }

        private static string[][] original, applied;
        private static int version, appliedVersion = -1;

        private static void Prefix(MegatenCanvas __instance)
        {
            var intro = __instance.intro;
            if (intro == null) return;
            if (intro == applied && appliedVersion == version) return;
            if (intro != applied) original = intro;            // first call (or the game rebuilt the array)
            var merged = new string[original.Length][];
            for (int i = 0; i < original.Length; i++)
                merged[i] = english != null && i < english.Length && english[i] != null ? english[i] : original[i];
            __instance.intro = applied = merged;
            appliedVersion = version;
        }
    }

    /// <summary>Bar rumors (barStringMsg, 17 entries): Bar_Main_Menu copies one into StringArray[3], then Bar_Draw types
    /// it out against StringArray[3].Length. Swapping it right after the copy (same frame, before any drawing) makes the
    /// typewriter count and the word wrap work on the English. Table: bar.tsv.</summary>
    [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.Bar_Main_Menu))]
    internal static class BarRumorPatch
    {
        private static readonly AccessTools.FieldRef<MegatenCanvas, string[]> StringArray =
            AccessTools.FieldRefAccess<MegatenCanvas, string[]>("StringArray");

        private static void Postfix(MegatenCanvas __instance)
        {
            var sa = StringArray(__instance);
            if (sa == null || sa.Length < 4 || string.IsNullOrEmpty(sa[3])) return;
            if (Strings.TryGet(sa[3], out var en) && en != sa[3]) sa[3] = en;
        }
    }

    /// <summary>Enhancement: reviving keeps the ally's MP. Skill_Effect 9 (revive a dying ally at 1/8 HP,
    /// e.g. Jin Dan) and 10 (revive at full HP, e.g. Balm of Life and the revive spells) both set Mp = 0. Items and
    /// spells, in battle and in the field, all go through Skill_Effect, so put the old MP back afterwards.</summary>
    [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.Skill_Effect))]
    internal static class ReviveKeepsMp
    {
        private static void Prefix(int id, CCharacter ch_tg, out int __state) =>
            __state = Plugin.ReviveKeepsMp.Value && (id == 9 || id == 10) && ch_tg != null ? ch_tg.Mp : -1;

        private static void Postfix(CCharacter ch_tg, int __state)
        {
            if (__state < 0 || ch_tg == null || ch_tg.Mp != 0) return;
            ch_tg.Mp = (short)Math.Min(__state, ch_tg.MaxMp);
        }
    }

    /// <summary>Enhancement (optional): no alignment limit on summoning. Iface_CheckSammon (the rule, used by the pause
    /// menu and in battle) and Iface_DrawPartyList (the grey-out) refuse a demon two steps away from the protagonist or
    /// from a demon already out, i.e. LAW against CHAOS. Those two methods are the only summon code that reads
    /// alignment, so while they run every character reads as NEUTRAL. Their MAG and already-summoned checks are kept.</summary>
    internal static class SummonAlignment
    {
        [ThreadStatic] internal static bool Neutral;

        internal static void Enter() => Neutral = Plugin.NoSummonAlignmentLimit.Value;
        internal static Exception Leave(Exception __exception)
        {
            Neutral = false;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.Iface_CheckSammon))]
    internal static class SummonAlignmentCheck
    {
        private static void Prefix() => SummonAlignment.Enter();
        private static Exception Finalizer(Exception __exception) => SummonAlignment.Leave(__exception);
    }

    [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.Iface_DrawPartyList),
        new[] { typeof(int), typeof(int), typeof(int), typeof(int) })]
    internal static class SummonAlignmentList
    {
        private static void Prefix() => SummonAlignment.Enter();
        private static Exception Finalizer(Exception __exception) => SummonAlignment.Leave(__exception);
    }

    [HarmonyPatch(typeof(CCharacter), nameof(CCharacter.AttributeId))]
    internal static class SummonAlignmentDemon
    {
        private static bool Prefix(ref sbyte __result)
        {
            if (!SummonAlignment.Neutral) return true;
            __result = 2;
            return false;
        }
    }

    [HarmonyPatch(typeof(CCharacter), nameof(CCharacter.HeroRace))]
    internal static class SummonAlignmentHero
    {
        private static bool Prefix(ref sbyte __result)
        {
            if (!SummonAlignment.Neutral) return true;
            __result = 2;
            return false;
        }
    }

    /// <summary>IFace_DrawBBoxName squeezes names over 80 px by drawing each character 3 px closer, which suits 12 px
    /// Japanese but piles up 6 px English. The enemy target name (y 144) has room, so it's drawn whole, centred where the
    /// caller meant it and kept on screen. The party row's columns are 80 px, so it uses the @narrow short form.</summary>
    [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.IFace_DrawBBoxName))]
    internal static class LongNamePatch
    {
        private const int TargetY = 144, ScreenW = 240, ColumnW = 80;

        private static bool Prefix(MegatenCanvas __instance, string name, int x, int y)
        {
            if (string.IsNullOrEmpty(name) || __instance.font == null) return true;
            int width = __instance.font.GetBBoxWidth(name);
            if (width <= ColumnW) return true;
            if (y == TargetY)
            {
                // The caller passes centre - 40 for wide names.
                int left = x + ColumnW / 2 - width / 2;
                __instance.g.DrawString(name, Math.Max(0, Math.Min(left, ScreenW - width)), y);
                return false;
            }
            if (!Strings.TryGet("@narrow." + name, out var shortName)) return true;
            __instance.g.DrawString(shortName, x, y);
            return false;
        }
    }

    /// <summary>Enhancement (optional): the minimap remembers walked tiles. Up to 30 tiles wide the trail follows the
    /// game's own 2 px grid; that grid merges too many tiles on bigger floors, so those use 1 px steps. Map_DrawField draws its markers last, so they are
    /// drawn again on top of the trail. Kept per chapter + map, written next to the game's saves as save\minitrail{slot}
    /// (key TAB base64 bitmap), so loading a save brings back the trail as it was at that save.</summary>
    [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.Map_DrawField))]
    internal static class MinimapTrail
    {
        private const int BoxX = 183, BoxY = 67, BoxSize = 48;
        private static readonly AccessTools.FieldRef<MegatenCanvas, sbyte> CountX = AccessTools.FieldRefAccess<MegatenCanvas, sbyte>("Map_ChipCountX");
        private static readonly AccessTools.FieldRef<MegatenCanvas, sbyte> CountY = AccessTools.FieldRefAccess<MegatenCanvas, sbyte>("Map_ChipCountY");
        private static readonly AccessTools.FieldRef<MegatenCanvas, sbyte> MapId = AccessTools.FieldRefAccess<MegatenCanvas, sbyte>("Sys_MapId");
        private static readonly AccessTools.FieldRef<MegatenCanvas, sbyte> Chapter = AccessTools.FieldRefAccess<MegatenCanvas, sbyte>("Sys_Chapter");
        private static readonly AccessTools.FieldRef<MegatenCanvas, int> HeroX = AccessTools.FieldRefAccess<MegatenCanvas, int>("Hero_PositionX");
        private static readonly AccessTools.FieldRef<MegatenCanvas, int> HeroY = AccessTools.FieldRefAccess<MegatenCanvas, int>("Hero_PositionY");

        private static readonly Dictionary<int, byte[]> floors = new Dictionary<int, byte[]>();
        private static readonly List<int[]> runs = new List<int[]>();
        private static int runsKey = -1;
        private static bool failed;

        private static string PathFor(int slot) => Path.Combine(Path.Combine(Paths.GameRootPath, "save"), "minitrail" + slot);

        private static void Postfix(MegatenCanvas __instance)
        {
            if (!Plugin.MinimapTrailOn.Value || failed) return;
            try { Draw(__instance); }
            catch (Exception e)
            {
                // The game swallows draw exceptions and drops the frame, so switch off instead of flickering.
                failed = true;
                Plugin.Log.LogError($"Minimap trail failed: {e}");
            }
        }

        private static void Draw(MegatenCanvas c)
        {
            int w = CountX(c), h = CountY(c);
            if (w <= 0 || h <= 0) return;
            int key = Chapter(c) * 256 + (MapId(c) & 0xFF);
            if (!floors.TryGetValue(key, out var seen) || seen.Length != (w * h + 7) / 8)
                floors[key] = seen = new byte[(w * h + 7) / 8];
            // The tile the game itself tests for stairs and events. Solid tiles are skipped: a frame can draw a new map
            // with the old position while changing floors.
            int here = c.Map_WorldToIndex(HeroX(c) + 12, HeroY(c) + 10);
            if (here >= 0 && (c.Map_GetFieldChipState(here) & int.MinValue) == 0 && (seen[here / 8] & (1 << here % 8)) == 0)
            {
                seen[here / 8] |= (byte)(1 << here % 8);
                runsKey = -1;
            }
            if (runsKey != key) Build(seen, w, h, key);
            c.g.SetColor(Color());
            foreach (var r in runs) c.g.FillRect(BoxX + r[0], BoxY + r[1], r[2], 1);
            DrawMarkers(c, w, h);
        }

        private static void Build(byte[] seen, int w, int h, int key)
        {
            var px = new bool[BoxSize, BoxSize];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if ((seen[i / 8] & (1 << i % 8)) == 0) continue;
                    Span(x, w, out int x0, out int x1);
                    Span(y, h, out int y0, out int y1);
                    for (int b = y0; b < y1 && b < BoxSize; b++)
                        for (int a = x0; a < x1 && a < BoxSize; a++) px[a, b] = true;
                }
            runs.Clear();
            for (int y = 0; y < BoxSize; y++)
                for (int x = 0; x < BoxSize; x++)
                {
                    if (!px[x, y]) continue;
                    int start = x;
                    while (x < BoxSize && px[x, y]) x++;
                    runs.Add(new[] { start, y, x - start });
                }
            runsKey = key;
        }

        private static void Span(int tile, int count, out int from, out int to)
        {
            if (count <= 24) { from = tile * 2; to = from + 2; return; }
            if (count <= 30) { from = tile * 24 / count * 2; to = from + 2; return; }
            from = tile * BoxSize / count;
            to = Math.Max(from + 1, (tile + 1) * BoxSize / count);
        }

        // The end of Map_DrawField again, same colours and positions.
        private static void DrawMarkers(MegatenCanvas c, int w, int h)
        {
            int hero = c.Map_WorldToIndex(HeroX(c), HeroY(c));
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x, state = c.Map_GetFieldChipState(i), color = 0;
                    if ((state & 1) != 0) color = StGraphics.GetColorOfRGB(255, 128, 128);
                    if ((state & 0x10) != 0) color = StGraphics.GetColorOfRGB(255, 255, 0);
                    if (i == hero) color = StGraphics.GetColorOfRGB(255, 255, 255);
                    if (color == 0) continue;
                    c.g.SetColor(color);
                    c.g.FillRect(w <= 24 ? BoxX + x * 2 : BoxX + x * 24 / w * 2, h <= 24 ? BoxY + y * 2 : BoxY + y * 24 / h * 2, 2, 2);
                }
        }

        private static int Color()
        {
            int rgb;
            try { rgb = Convert.ToInt32(Plugin.MinimapTrailColor.Value.Trim(), 16); }
            catch (Exception) { rgb = 0x6084DC; }
            return StGraphics.GetColorOfRGB((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }

        internal static void Save(int slot)
        {
            try { File.WriteAllLines(PathFor(slot), floors.Select(f => f.Key + "\t" + Convert.ToBase64String(f.Value)).ToArray()); }
            catch (Exception e) { Plugin.Log.LogError($"Minimap trail save failed: {e.Message}"); }
        }

        internal static void Load(int slot)
        {
            Clear();
            try
            {
                if (!File.Exists(PathFor(slot))) return;
                foreach (var line in File.ReadAllLines(PathFor(slot)))
                {
                    var parts = line.Split('\t');
                    if (parts.Length == 2) floors[int.Parse(parts[0])] = Convert.FromBase64String(parts[1]);
                }
            }
            catch (Exception e) { Plugin.Log.LogError($"Minimap trail load failed: {e.Message}"); }
        }

        internal static void Clear()
        {
            floors.Clear();
            runsKey = -1;
        }
    }

    [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.Save_GameData))]
    internal static class MinimapTrailSave
    {
        private static void Postfix(int id) { if (Plugin.MinimapTrailOn.Value) MinimapTrail.Save(id); }
    }

    [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.Load_GameData))]
    internal static class MinimapTrailLoad
    {
        private static void Postfix(int id) => MinimapTrail.Load(id);
    }

    [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.Event_NewGameInt))]
    internal static class MinimapTrailNewGame
    {
        private static void Postfix() => MinimapTrail.Clear();
    }

    /// <summary>Enhancement (optional): EXP / money / MAG multipliers. Battle_GetExp runs once per defeated (or sealed)
    /// enemy and adds its share to GenWorkInt[9] (EXP), [10] (yen) and [11] (MAG); the results screen shows and pays
    /// those totals, so scaling each enemy's share keeps the screen and the payout in step.</summary>
    [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.Battle_GetExp))]
    internal static class RewardMultiplier
    {
        private static void Prefix(MegatenCanvas __instance, out int[] __state)
        {
            var w = __instance.GenWorkInt;
            __state = new[] { w[9], w[10], w[11] };
        }

        private static void Postfix(MegatenCanvas __instance, int[] __state)
        {
            var w = __instance.GenWorkInt;
            w[9] = Scale(__state[0], w[9], Plugin.ExpRate.Value);
            w[10] = Scale(__state[1], w[10], Plugin.MoneyRate.Value);
            w[11] = Scale(__state[2], w[11], Plugin.MagRate.Value);
        }

        private static int Scale(int before, int after, int rate)
        {
            long gained = (long)(after - before) * Math.Max(1, rate);
            return (int)Math.Min(int.MaxValue, before + gained);
        }
    }

    /// <summary>Enhancement (optional, NoSpellFlash): every spell (magic command) plays Battle_DrawSpecailEffect, a
    /// full-screen white rectangle on every other frame, while physical attacks play Battle_DrawAttackEffect (two
    /// white bars closing in on the target group). Play the bars for spells too, so nothing flashes.</summary>
    [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.Battle_DrawSpecailEffect))]
    internal static class SpellFlashToBars
    {
        private static bool Prefix(MegatenCanvas __instance)
        {
            if (!Plugin.NoSpellFlash.Value) return true;
            __instance.Battle_DrawAttackEffect();
            return false;
        }
    }

    /// <summary>Enhancement (optional, DemonCompendium): recorded demons. A record is the game's own 283-byte party
    /// save record (CCharacter.MakeSaveData: name, level, EXP, HP/MP, stats, skills, flags); SetLoadData restores it
    /// and refills affinity / antibody / image from the base data, exactly as loading a save does. One record per
    /// demon id, kept in memory and written next to the game's saves as save\compendium{slot} (id TAB base64).</summary>
    internal static class Compendium
    {
        private const int LevelOffset = 18;   // 16-byte name + 2-byte id
        private static readonly Dictionary<int, sbyte[]> records = new Dictionary<int, sbyte[]>();

        public static bool Enabled => Plugin.DemonCompendium.Value;

        private static string PathFor(int slot) => Path.Combine(Path.Combine(Paths.GameRootPath, "save"), "compendium" + slot);

        public static bool TryGet(int id, out sbyte[] data) => records.TryGetValue(id, out data);

        public static int RecordedLevel(int id) => records.TryGetValue(id, out var d) ? d[LevelOffset] : -1;

        private const int StatsOffset = 31, SkillsOffset = 37;   // 6 stat bytes (St In Ma Vi Ag Lu), then 8 skill ids

        /// <summary>True when the demon's stats or skill set (in any order) differ from its record, e.g. a fresh fusion
        /// with other inherited skills, or incense used since recording.</summary>
        public static bool DiffersFromRecord(CCharacter c)
        {
            if (!records.TryGetValue(c.Id, out var d)) return true;
            var now = c.MakeSaveData();
            for (int i = 0; i < 6; i++)
                if (now[StatsOffset + i] != d[StatsOffset + i]) return true;
            var a = new List<sbyte>(); var b = new List<sbyte>();
            for (int i = 0; i < 8; i++) { a.Add(now[SkillsOffset + i]); b.Add(d[SkillsOffset + i]); }
            a.Sort(); b.Sort();
            for (int i = 0; i < 8; i++) if (a[i] != b[i]) return true;
            return false;
        }

        public static void Record(CCharacter c) => records[c.Id] = c.MakeSaveData();

        public static void Clear() => records.Clear();

        public static void Save(int slot)
        {
            try
            {
                var lines = new List<string>();
                foreach (var kv in records)
                {
                    var bytes = new byte[kv.Value.Length];
                    Buffer.BlockCopy(kv.Value, 0, bytes, 0, bytes.Length);
                    lines.Add(kv.Key + "\t" + Convert.ToBase64String(bytes));
                }
                File.WriteAllLines(PathFor(slot), lines.ToArray());
            }
            catch (Exception e) { Plugin.Log.LogError($"Compendium save failed: {e.Message}"); }
        }

        public static void Load(int slot)
        {
            records.Clear();
            var path = PathFor(slot);
            if (!File.Exists(path)) return;
            try
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    var cols = line.Split('\t');
                    if (cols.Length != 2 || !int.TryParse(cols[0], out var id)) continue;
                    var bytes = Convert.FromBase64String(cols[1]);
                    var data = new sbyte[bytes.Length];
                    Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);
                    records[id] = data;
                }
            }
            catch (Exception e) { Plugin.Log.LogError($"Compendium load failed: {e.Message}"); }
        }

        public static string L(string key, string fallback) => Strings.TryGet(key, out var v) ? v : fallback;
    }

    [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.Save_GameData))]
    internal static class CompendiumSave
    {
        private static void Postfix(int id) { if (Compendium.Enabled) Compendium.Save(id); }
    }

    [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.Load_GameData))]
    internal static class CompendiumLoad
    {
        private static void Postfix(int id) => Compendium.Load(id);
    }

    [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.Event_NewGameInt))]
    internal static class CompendiumNewGame
    {
        private static void Postfix() => Compendium.Clear();
    }

    /// <summary>The Cathedral menu gets a 6th entry, "Compendium", before Back (Basic_Union drives a 5-entry command
    /// window: 0 Fusion, 1 Sword Fusion, 2 Summon, 3 Talk, 4 Back). While the Compendium screen is open it replaces
    /// Basic_Union (input) and Union_BasicDraw (drawing), so the game stays in its own step 802 throughout.</summary>
    internal static class CompendiumMenu
    {
        private enum Screen { Closed, List, Confirm, Done, Empty }
        private const int Left = 65536, Up = 131072, Right = 262144, Down = 524288, Ok = 1048576, Cancel = 2097152;

        private static Screen screen;
        private static int cursor, yesNo;
        private static string doneName;
        private static readonly List<int> rows = new List<int>();
        internal static bool InBasic, InBasicDraw;

        internal static readonly AccessTools.FieldRef<MegatenCanvas, string[]> StringArray =
            AccessTools.FieldRefAccess<MegatenCanvas, string[]>("StringArray");

        internal static bool Open => screen != Screen.Closed;

        internal static void Start(MegatenCanvas m)
        {
            BuildRows(m);
            cursor = 0;
            screen = rows.Count == 0 ? Screen.Empty : Screen.List;
        }

        // Party demons (slots 1-6) not yet recorded, recorded at a lower level than they are now, or whose skills or
        // stats differ from the record (a re-fused demon with other skills can replace the record at any level)
        private static void BuildRows(MegatenCanvas m)
        {
            rows.Clear();
            for (int i = 1; i < 7; i++)
            {
                var c = m.Party_Work[i];
                if (c != null && c.Id != 0 && c.Id != 255
                    && (Compendium.RecordedLevel(c.Id) < c.Level || Compendium.DiffersFromRecord(c))) rows.Add(i);
            }
            if (cursor >= rows.Count) cursor = Math.Max(0, rows.Count - 1);
        }

        internal static void Update(MegatenCanvas m)
        {
            switch (screen)
            {
                case Screen.List:
                    if (m.GetKeyRepeat(Up)) { m.SetSE(3); cursor = (cursor + rows.Count - 1) % rows.Count; }
                    else if (m.GetKeyRepeat(Down)) { m.SetSE(3); cursor = (cursor + 1) % rows.Count; }
                    else if (m.GetKeyTrg(Ok)) { m.SetSE(4); yesNo = 0; screen = Screen.Confirm; }
                    else if (m.GetKeyTrg(Cancel)) { m.SetSE(5); screen = Screen.Closed; }
                    break;
                case Screen.Confirm:
                    if (m.GetKeyRepeat(Left) || m.GetKeyRepeat(Right)) { m.SetSE(3); yesNo ^= 1; }
                    else if (m.GetKeyTrg(Ok))
                    {
                        m.SetSE(4);
                        if (yesNo == 0)
                        {
                            var c = m.Party_Work[rows[cursor]];
                            Compendium.Record(c);
                            doneName = c.Name;
                            screen = Screen.Done;
                        }
                        else screen = Screen.List;
                    }
                    else if (m.GetKeyTrg(Cancel)) { m.SetSE(5); screen = Screen.List; }
                    break;
                case Screen.Done:
                    if (m.GetKeyTrg(Ok) || m.GetKeyTrg(Cancel))
                    {
                        m.SetSE(4);
                        BuildRows(m);
                        screen = rows.Count == 0 ? Screen.Closed : Screen.List;
                    }
                    break;
                case Screen.Empty:
                    if (m.GetKeyTrg(Ok) || m.GetKeyTrg(Cancel)) { m.SetSE(5); screen = Screen.Closed; }
                    break;
            }
        }

        internal static void Draw(MegatenCanvas m)
        {
            var g = m.g;
            var font = m.font;
            int h = font.GetHeight(), rowH = h + 2;
            m.Iface_SetSoftKey1ReturnMsg(set: false);
            if (rows.Count > 0 && screen != Screen.Empty)
            {
                m.Iface_DrawGenWindow(5, 42, 230, rowH * rows.Count + 4);
                m.Iface_DrawSquareCursor(7, 44 + cursor * rowH, 226, rowH);
                g.SetColor(StGraphics.GetColorOfRGB(255, 255, 255));
                for (int i = 0; i < rows.Count; i++)
                {
                    var c = m.Party_Work[rows[i]];
                    int y = 46 + font.GetAscent() + i * rowH;
                    g.DrawString(c.Name, 12, y);
                    g.DrawString("Lv" + c.Level, 140, y);
                    int rec = Compendium.RecordedLevel(c.Id);
                    if (rec >= 0) g.DrawString(string.Format(Compendium.L("@comp.reclv", "(Rec {0})"), rec), 176, y);
                }
            }
            m.Iface_DrawGenWindow(0, 130, 240, 65);
            g.SetColor(StGraphics.GetColorOfRGB(255, 255, 255));
            int ty = 130 + h + 4;
            switch (screen)
            {
                case Screen.List:
                    g.DrawString(Compendium.L("@comp.pick", "Which demon will you record?"), 5, ty);
                    break;
                case Screen.Confirm:
                    var c = m.Party_Work[rows[cursor]];
                    g.DrawString(string.Format(Compendium.L("@comp.ask", "Record {0} at Lv {1}?"), c.Name, c.Level), 5, ty);
                    int old = Compendium.RecordedLevel(c.Id);
                    if (old >= 0)
                        g.DrawString(string.Format(Compendium.L(old > c.Level ? "@comp.replacehigher" : "@comp.replace",
                            old > c.Level ? "Replaces the Lv {0} record!" : "Replaces the Lv {0} record."), old), 5, ty + 15);
                    g.DrawString(Compendium.L("@comp.yes", "Yes"), 55, ty + 30);
                    g.DrawString(Compendium.L("@comp.no", "No"), 130, ty + 30);
                    m.Iface_DrawSquareCursor(40 + yesNo * 80, ty + 30 - h, 58, h + 2);
                    break;
                case Screen.Done:
                    g.DrawString(string.Format(Compendium.L("@comp.done", "{0} has been recorded."), doneName), 5, ty);
                    break;
                case Screen.Empty:
                    g.DrawString(Compendium.L("@comp.empty", "There are no demons to record."), 5, ty);
                    break;
            }
        }
    }

    [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.Basic_Union))]
    internal static class CompendiumBasicUnion
    {
        private static bool Prefix(MegatenCanvas __instance)
        {
            if (!Compendium.Enabled) return true;
            if (CompendiumMenu.Open)
            {
                CompendiumMenu.Update(__instance);
                __instance.GenWorkShort[0] = 802;
                return false;
            }
            CompendiumMenu.InBasic = true;
            return true;
        }

        private static void Finalizer() => CompendiumMenu.InBasic = false;
    }

    [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.Iface_ControlCommandWindow))]
    internal static class CompendiumCommandControl
    {
        private static void Prefix(ref int command_max)
        {
            if (CompendiumMenu.InBasic && command_max == 5) command_max = 6;
        }

        private static void Postfix(MegatenCanvas __instance, ref int __result)
        {
            if (!CompendiumMenu.InBasic) return;
            if (__result == 4) { CompendiumMenu.Start(__instance); __result = -1; }
            else if (__result == 5) __result = 4;   // Back moved down one
        }
    }

    [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.Union_BasicDraw))]
    internal static class CompendiumBasicDraw
    {
        private static bool Prefix(MegatenCanvas __instance)
        {
            if (!Compendium.Enabled) return true;
            if (CompendiumMenu.Open)
            {
                CompendiumMenu.Draw(__instance);
                return false;
            }
            CompendiumMenu.InBasicDraw = true;
            return true;
        }

        private static void Finalizer() => CompendiumMenu.InBasicDraw = false;
    }

    /// <summary>Union_BasicDraw draws StringArray[22..26]; for the 6-entry menu, show 22..27 with "Compendium" put in
    /// at 26 and Back moved to 27, then put both strings back.</summary>
    [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.Iface_DrawCommandWindow))]
    internal static class CompendiumCommandDraw
    {
        private static void Prefix(MegatenCanvas __instance, int str_index_s, ref int str_index_e, out string[] __state)
        {
            __state = null;
            if (!CompendiumMenu.InBasicDraw || str_index_s != 22 || str_index_e != 26) return;
            var sa = CompendiumMenu.StringArray(__instance);
            if (sa == null || sa.Length < 28) return;
            __state = new[] { sa[26], sa[27] };
            sa[27] = sa[26];
            sa[26] = Compendium.L("@comp.menu", "Compendium");
            str_index_e = 27;
        }

        private static void Finalizer(MegatenCanvas __instance, string[] __state)
        {
            if (__state == null) return;
            var sa = CompendiumMenu.StringArray(__instance);
            sa[26] = __state[0];
            sa[27] = __state[1];
        }
    }

    /// <summary>Summoning: on the demon's info screen (Union_SamonSetAfter, GenWorkShort[4] == 1), Left/Right swaps
    /// slot 7 between the base demon and its record. A recorded demon arrives fresh (full HP/MP, no ailments). With
    /// cost scaling, its antibody (cost = antibody x 50; also the per-step MAG upkeep) is raised by recorded / base
    /// level while it's on offer, and put back to the base value as soon as the MAG is paid.</summary>
    internal static class CompendiumSummon
    {
        internal static bool ShowingRecord;
        private static sbyte baseAntibody;
        private static bool paid;

        [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.Union_Samon))]
        internal static class Select
        {
            private static void Postfix(MegatenCanvas __instance)
            {
                if (__instance.GenWorkShort[0] == 821) { ShowingRecord = false; paid = false; }
            }
        }

        [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.Union_SamonSetAfter))]
        internal static class Toggle
        {
            private static void Prefix(MegatenCanvas __instance)
            {
                if (!Compendium.Enabled || __instance.GenWorkShort[4] != 1) return;
                var c = __instance.Party_Work[7];
                if (c == null || !Compendium.TryGet(c.Id, out var snap)) return;
                if (!__instance.GetKeyTrg(65536) && !__instance.GetKeyTrg(262144)) return;
                __instance.SetSE(3);
                if (ShowingRecord)
                {
                    c.SetData(__instance.samonDevilBin[__instance.GenWorkShort[2]]);
                    ShowingRecord = false;
                    return;
                }
                int baseLevel = Math.Max(1, (int)c.Level);
                baseAntibody = c.antibody;
                c.SetLoadData((sbyte[])snap.Clone());
                c.Hp = c.MaxHp;
                c.Mp = c.MaxMp;
                c.Flag = 0;
                c.statusCount = 0;
                c.dopingFlag = false;
                if (Plugin.CompendiumCostScales.Value)
                    c.antibody = (sbyte)Math.Min(127, Math.Max(baseAntibody, (int)Math.Round(baseAntibody * (double)c.Level / baseLevel)));
                ShowingRecord = true;
                paid = false;
            }

            private static void Postfix(MegatenCanvas __instance)
            {
                if (!ShowingRecord || paid || (__instance.GenWorkShort[4] & 0x10) == 0) return;
                __instance.Party_Work[7].antibody = baseAntibody;   // paid: back to the normal MAG upkeep
                paid = true;
            }
        }

        [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.Union_SamonSetAfterDraw))]
        internal static class Label
        {
            private static void Postfix(MegatenCanvas __instance)
            {
                if (!Compendium.Enabled || (__instance.GenWorkShort[4] & 0x10) != 0) return;
                var c = __instance.Party_Work[7];
                if (c == null || !Compendium.TryGet(c.Id, out _)) return;
                var text = ShowingRecord ? Compendium.L("@comp.rec", "← Recorded →") : Compendium.L("@comp.base", "← Base →");
                var font = __instance.font;
                int w = font.StringWidth(text) + 12, h = font.GetHeight() + 4;
                int x = (240 - w) / 2, y = 23;
                __instance.Iface_DrawGenWindow(x, y, w, h);
                __instance.g.SetColor(ShowingRecord ? StGraphics.GetColorOfRGB(127, 255, 255) : StGraphics.GetColorOfRGB(255, 255, 255));
                __instance.g.DrawString(text, x + 6, y + 2 + font.GetAscent());
            }
        }
    }

    /// <summary>The Cathedral's Talk speech: Union_Initialize sets StringArray[17] (a long "\n"-paged text), which
    /// Basic_Union copies into StringArray[18] for Union_Talk to type out. Same fix as the Bar: swap it at init.</summary>
    [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.Union_Initialize))]
    internal static class CathedralTalkPatch
    {
        private static readonly AccessTools.FieldRef<MegatenCanvas, string[]> StringArray =
            AccessTools.FieldRefAccess<MegatenCanvas, string[]>("StringArray");

        private static void Postfix(MegatenCanvas __instance)
        {
            var sa = StringArray(__instance);
            if (sa == null || sa.Length < 18 || string.IsNullOrEmpty(sa[17])) return;
            if (Strings.TryGet(sa[17], out var en) && en != sa[17]) sa[17] = en;
        }
    }

    [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.DrawMessage),
        new[] { typeof(string), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int) })]
    internal static class DrawMessageWrapPatch
    {
        private static void Prefix(MegatenCanvas __instance, ref string mes, int _x, int turnNum) =>
            mes = Wrap.Apply(mes, turnNum - _x, __instance.font);
    }

    [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.EventDrawMessage))]
    internal static class EventDrawMessageWrapPatch
    {
        private static void Prefix(MegatenCanvas __instance, ref string mes, int _x, int turnNum) =>
            mes = Wrap.Apply(mes, turnNum - _x, __instance.font);
    }

    /// <summary>Story events (chapN_megarep020 → event*.bin): opcode 6 (text box) and 31 (full-screen narration) carry
    /// two SJIS strings, speaker then text, which EventMsg reads into StringArray[0..1] and fills in @YOUNAME etc.
    /// We re-read the raw strings, look them up (speakers as "@spk.{jp}" first, so short names like 男 don't leak
    /// into other screens), and redo the substitutions on the English. Keys keep the tokens and use "\n" for the
    /// data's backslash-n. In the English, "\p" starts a new text box (padded to the box's 3 lines).</summary>
    [HarmonyPatch(typeof(MegatenCanvas), nameof(MegatenCanvas.EventMsg))]
    internal static class EventTextPatch
    {
        private const int BoxPx = 228, NarrationPx = 200, BoxLines = 3;
        private static readonly AccessTools.FieldRef<MegatenCanvas, string[]> StringArray =
            AccessTools.FieldRefAccess<MegatenCanvas, string[]>("StringArray");
        private static readonly AccessTools.FieldRef<MegatenCanvas, sbyte> SysChapter =
            AccessTools.FieldRefAccess<MegatenCanvas, sbyte>("Sys_Chapter");

        private static void Prefix(MegatenCanvas __instance, out int __state) => __state = __instance.GenWorkShort[1];

        private static void Postfix(MegatenCanvas __instance, int __state)
        {
            var bin = __instance.eventBin;
            var sa = StringArray(__instance);
            if (bin == null || sa == null) return;
            bool narration = __instance.GenWorkShort[2] == 31;
            int p = __state;
            for (int i = 0; i < 2; i++)
            {
                int len = 0;
                while (p + len < bin.Length && bin[p + len] != 0) len++;
                var raw = SocotraRuntime.GetStringForBytesFromSjis(bin, p, len).Replace("\\n", "\n");
                p += len + 1;
                if (raw.Length == 0) continue;
                string en;
                if (!(i == 0 && Strings.TryGet("@spk." + raw, out en)) && !Strings.TryGet(raw, out en))
                {
                    Strings.NoteMissing(raw, i == 0 ? "Event(speaker)" : "Event");
                    continue;
                }
                en = Fill(__instance, en);
                if (i == 1) en = Paginate(en, narration ? NarrationPx : BoxPx, __instance.font, narration ? 0 : BoxLines);
                sa[i] = en;
            }
        }

        private static string Fill(MegatenCanvas c, string s)
        {
            int chapter = SysChapter(c);
            s = s.Replace("@YOUNAME", c.Party_Work[0]?.Name ?? string.Empty).Replace("@HEROINE", c.HeroineName ?? string.Empty);
            return s.Replace("NAWCHAP", (chapter + 1).ToString()).Replace("OKDL", ((chapter + 1) * 30 - c.netSeqInt).ToString());
        }

        /// <summary>"\p" → enough blank lines to finish the current box; in narration (one screen) just a blank line.</summary>
        private static string Paginate(string s, int maxPx, StFont font, int perPage)
        {
            if (s.IndexOf("\\p", StringComparison.Ordinal) < 0) return s;
            var parts = s.Split(new[] { "\\p" }, StringSplitOptions.None);
            var sb = new StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                var part = Wrap.Apply(parts[i].Trim(' '), maxPx, font);
                sb.Append(part);
                if (i == parts.Length - 1) break;
                int lines = part.Split('\n').Length;
                int pad = perPage > 0 ? (perPage - lines % perPage) % perPage : 1;
                sb.Append('\n', pad + 1);
            }
            return sb.ToString();
        }
    }

    /// <summary>G-MODE's name-entry dialog (uGUI, not StGraphics): the prompt comes in through Init...</summary>
    [HarmonyPatch(typeof(CharacterInputDialog), nameof(CharacterInputDialog.Init))]
    internal static class NameEntryPromptPatch
    {
        private static void Prefix(ref string message)
        {
            if (!string.IsNullOrEmpty(message) && Strings.TryGet(message, out var en)) message = en;
            else if (message != null) Strings.NoteMissing(message, "CharacterInputDialog");
        }
    }

    /// <summary>G-MODE's yes/no popup. Most callers pass Localization strings (already English);
    /// the name-entry confirm passes a hardcoded 「{name}」で宜しいですか？ (see patterns_wrapper.tsv).</summary>
    [HarmonyPatch(typeof(CommonDialog), nameof(CommonDialog.Init),
        new[] { typeof(string), typeof(UnityEngine.Events.UnityAction), typeof(UnityEngine.Events.UnityAction) })]
    internal static class CommonDialogPatch
    {
        private static void Prefix(ref string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (Strings.TryGet(message, out var en)) message = en;
            else Strings.NoteMissing(message, "CommonDialog");
        }
    }

    /// <summary>Translates text baked into G-MODE prefabs (uGUI Text / TextMeshPro) that no localization key covers,
    /// e.g. フルスクリーン in the Window dialog. Add more dialog types to the TargetMethods list as they turn up.</summary>
    [HarmonyPatch]
    internal static class PrefabTextSweep
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            var m = AccessTools.Method(typeof(WindowDialog), "Start");
            if (m != null) yield return m;
        }

        private static void Postfix(UnityEngine.Component __instance) => Sweep(__instance);

        public static void Sweep(UnityEngine.Component root)
        {
            if (root == null) return;
            foreach (var t in root.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                if (!string.IsNullOrEmpty(t.text) && Strings.TryGet(t.text, out var en)) t.text = en;
            foreach (var t in root.GetComponentsInChildren<TMPro.TMP_Text>(true))
                if (!string.IsNullOrEmpty(t.text) && Strings.TryGet(t.text, out var en)) t.text = en;
        }
    }

    /// <summary>...and the 削除/決定 key captions through CharacterInputKeyItem.Init (display only; the key actions ignore it).</summary>
    [HarmonyPatch(typeof(CharacterInputKeyItem), nameof(CharacterInputKeyItem.Init))]
    internal static class NameEntryKeyPatch
    {
        private static void Prefix(ref string keyString)
        {
            if (!string.IsNullOrEmpty(keyString) && keyString.Length > 1 && Strings.TryGet(keyString, out var en)) keyString = en;
        }
    }

    /// <summary>The affinity line ("破魔・緊縛・突撃・針に強い") is assembled from Affinity[] with Japanese suffixes.
    /// Rebuild it SMT IV style: "Res: Light/Bind/Rush/Needle", groups split by two spaces, abbreviated if too wide.
    /// Affinity codes (from Init_CompatibilityExplanation): 0 null, 1-4 resist, 9-12 weak, 13-14 repel, 15 drain.
    /// Index 1 (Gun) is skipped there too.</summary>
    [HarmonyPatch(typeof(CCharacter), nameof(CCharacter.Init_CompatibilityExplanation))]
    internal static class AffinityLinePatch
    {
        private const int MaxChars = 36;
        private static readonly string[] Elements = { "Sword", "Gun", "Fire", "Ice", "Elec", "Force", "Nerve", "Light", "Dark", "Magic", "Bind", "Rush", "Skill", "Needle", "Almighty" };
        private static readonly string[] Groups = { "Null", "Res", "Weak", "Repel", "Drain" };
        private static readonly string[] Short = { "Nu", "Rs", "Wk", "Rp", "Dr" };

        private static int GroupOf(int a)
        {
            if (a == 0) return 0;
            if (a >= 1 && a <= 4) return 1;
            if (a >= 9 && a <= 12) return 2;
            if (a == 13 || a == 14) return 3;
            if (a == 15) return 4;
            return -1;
        }

        private static void Postfix(CCharacter __instance)
        {
            var aff = __instance.Affinity;
            if (aff == null) return;
            var lists = new List<string>[Groups.Length];
            for (int i = 0; i < 15 && i < aff.Length; i++)
            {
                if (i == 1) continue;
                int g = GroupOf(aff[i]);
                if (g < 0) continue;
                (lists[g] ?? (lists[g] = new List<string>())).Add(Label("@res.elem." + i, Elements[i]));
            }
            string Build(bool abbreviated, string colon)
            {
                var parts = new List<string>();
                for (int g = 0; g < Groups.Length; g++)
                    if (lists[g] != null)
                        parts.Add((abbreviated ? Short[g] : Label("@res.group." + g, Groups[g])) + colon + string.Join("/", lists[g].ToArray()));
                return string.Join("  ", parts.ToArray());
            }
            var line = Build(false, ": ");
            if (line.Length > MaxChars) line = Build(false, ":");
            if (line.Length > MaxChars) line = Build(true, ":");
            __instance.CompatibilityExplanation = line;
        }

        private static string Label(string key, string fallback) => Strings.TryGet(key, out var v) ? v : fallback;
    }

    /// <summary>howToPlayImagesEn is empty in this build, so English mode showed a blank page.
    /// Fall back to the Japanese pages (art is out of scope for now; translated images can replace them later).</summary>
    [HarmonyPatch(typeof(HowToPlayDataModel), nameof(HowToPlayDataModel.GetHowToPlayImages))]
    internal static class HowToPlayFallbackPatch
    {
        private static void Postfix(HowToPlayDataModel __instance, ref UnityEngine.Sprite[] __result)
        {
            bool empty = __result == null || __result.Length == 0;
            if (!empty)
            {
                empty = true;
                foreach (var s in __result) if (s != null) { empty = false; break; }
            }
            if (empty)
                __result = Traverse.Create(__instance).Field("howToPlayImages").GetValue<UnityEngine.Sprite[]>() ?? __result;
        }
    }

    /// <summary>G-MODE left the English title data as a template ("Archives Title", "XXXXXXX").
    /// Serve the Japanese entry (logo, music, screenshot) with our English text from the "@title.*" keys.</summary>
    [HarmonyPatch(typeof(TitleDataModel), nameof(TitleDataModel.GetTitleData))]
    internal static class TitleDataPatch
    {
        private static TitleDataModel.TitleData english;

        private static void Postfix(TitleDataModel __instance, ref TitleDataModel.TitleData __result)
        {
            if (Localization.language == "ja") return;
            if (english == null)
            {
                var jp = Traverse.Create(__instance).Field("titleData").GetValue<TitleDataModel.TitleData>();
                if (jp == null) return;
                var en = __result;
                english = new TitleDataModel.TitleData();
                foreach (var f in AccessTools.GetDeclaredFields(typeof(TitleDataModel.TitleData)))
                {
                    // Keep G-MODE's English art (e.g. the "G-MODE Archives+" logo) where it exists; take the rest from JP.
                    var enValue = en != null && typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType) ? f.GetValue(en) as UnityEngine.Object : null;
                    f.SetValue(english, enValue != null ? enValue : f.GetValue(jp));
                }
                Set("@title.name", v => english.tilteName = v);
                Set("@title.about", v => english.aboutGame = v);
                Set("@title.copyright", v => english.copyright = v);
                Set("@title.release", v => english.releaseData = v);
            }
            __result = english;
        }

        private static void Set(string key, Action<string> apply)
        {
            if (Strings.TryGet(key, out var v)) apply(v);
        }
    }
}
