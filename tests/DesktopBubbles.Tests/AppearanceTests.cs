using DesktopBubbles.Core;

internal static class AppearanceTests
{
    public static void Run(Action<string, Action> test, Action<bool> assert, string fixtureRoot)
    {
        test("Modus-Deckkraft bleibt getrennt, wird begrenzt und beeinflusst nur Hintergrund-Alpha", () =>
        {
            var state = new BubbleState { Settings = new BubbleSettings { LightModeColor = "#FFEEDD", LightModeOpacity = .4, DarkModeOpacity = 1 } };
            var light = ThemePalette.Create(state.Settings, false); var dark = ThemePalette.Create(state.Settings, true);
            assert(light.ControlFill.StartsWith("#66") && light.Ink == "#000000" && dark.ControlFill == "#383348");
            string path = Path.Combine(fixtureRoot, "mode-opacity.json");
            try { new StateStore(path).Save(state); var loaded = StateStore.Read(path); assert(loaded.Settings.LightModeOpacity == .4 && loaded.Settings.DarkModeOpacity == 1); }
            finally { File.Delete(path); }
            state.Settings.LightModeOpacity = 0; assert(ThemePalette.Create(state.Settings, false).ControlFill.StartsWith("#00"));
            state.Settings.LightModeOpacity = double.NaN; state.Settings.DarkModeOpacity = -2; StateStore.Validate(state); assert(state.Settings.LightModeOpacity == 1 && state.Settings.DarkModeOpacity == 0);
        });
        test("Alte Konfiguration behält Modusfarben; ungültige Overrides fallen auf den Standard zurück", () =>
        {
            var state = new BubbleState(); StateStore.Validate(state);
            assert(state.Settings.LightModeColor is null && state.Settings.DarkModeColor is null && ThemePalette.Create(state.Settings, false).ControlFill == "#70FFFFFF" && ThemePalette.Create(state.Settings, true).MaterialBasis == "#1E1B2B");
            state.Settings.LightModeColor = "12a3f0"; state.Settings.DarkModeColor = "invalid"; StateStore.Validate(state);
            assert(state.Settings.LightModeColor == "#12A3F0" && state.Settings.DarkModeColor is null);
        });
        test("Helle und dunkle Modusfarben werden unabhängig gespeichert und gemeinsam zurückgesetzt", () =>
        {
            string path = Path.Combine(fixtureRoot, "mode-colors.json");
            try
            {
                var state = new BubbleState { Settings = new BubbleSettings { LightModeColor = "#FFEEDD", DarkModeColor = "#112233", AppColor = "#123456", Theme = "System" } };
                new StateStore(path).Save(state); var loaded = StateStore.Read(path);
                assert(loaded.Settings.LightModeColor == "#FFEEDD" && loaded.Settings.DarkModeColor == "#112233" && loaded.Settings.AppColor == "#123456" && loaded.Settings.Theme == "System");
                AppearancePreferences.ResetColors(loaded); assert(loaded.Settings.LightModeColor is null && loaded.Settings.DarkModeColor is null && loaded.Settings.Theme == "System");
            }
            finally { File.Delete(path); }
        });
        test("Eigene Modusfarben erhalten lesbaren Text auf Flächen und Steuerelementen", () =>
        {
            static double Light(string hex)
            {
                ColorValue.TryParse(hex, out var c);
                static double Linear(byte v) { double s = v / 255d; return s <= .04045 ? s / 12.92 : Math.Pow((s + .055) / 1.055, 2.4); }
                return .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B);
            }
            for (int r = 0; r < 256; r += 51) for (int g = 0; g < 256; g += 51) for (int b = 0; b < 256; b += 51)
            {
                var palette = ThemePalette.Create(new BubbleSettings { DarkModeColor = new ColorValue((byte)r, (byte)g, (byte)b).Hex }, true);
                foreach (string surface in new[] { palette.Surface, palette.ControlFill, palette.HoverFill })
                { double a = Light(surface), t = Light(palette.Ink); assert((Math.Max(a, t) + .05) / (Math.Min(a, t) + .05) >= 4.5); }
            }
        });
        test("Freie HEX-Farbe statt Presets normalisieren", () => assert(ColorValue.Normalize("#12a3f0") == "#12A3F0" && ColorValue.Normalize("000000") == "#000000" && !ColorValue.TryParse("#GGGGGG", out _) && !ColorValue.TryParse("#80FF00FF", out _)));
        test("HSV erlaubt weiß, grau und schwarz zusätzlich zu allen Farbtönen", () =>
        {
            assert(ColorValue.FromHsv(120, 1, 1).Hex == "#00FF00" && ColorValue.FromHsv(240, 1, 1).Hex == "#0000FF");
            assert(ColorValue.FromHsv(77, 0, 1).Hex == "#FFFFFF" && ColorValue.FromHsv(77, 0, .5).Hex == "#808080" && ColorValue.FromHsv(77, 1, 0).Hex == "#000000");
        });
        test("HEX-HSV-Roundtrip über den RGB-Würfel", () =>
        {
            for (int r = 0; r < 256; r += 17) for (int g = 0; g < 256; g += 17) for (int b = 0; b < 256; b += 17)
            { var color = new ColorValue((byte)r, (byte)g, (byte)b); var hsv = color.ToHsv(); assert(ColorValue.FromHsv(hsv.H, hsv.S, hsv.V) == color); }
        });
        test("Schema 2 migriert alte Defaults, erhält eigene Werte und Einträge", () =>
        {
            var state = new BubbleState { Version = 2, Settings = new BubbleSettings { Theme = "System", Columns = 4, Opacity = .78 } }; StateStore.Validate(state);
            assert(state.Version == 6 && state.Settings.Columns == 3 && state.Settings.Opacity == .35 && state.Settings.Theme == "Light");
            var custom = new BubbleState { Version = 2, Settings = new BubbleSettings { Theme = "Dark", Columns = 5, Opacity = .5 } }; StateStore.Validate(custom);
            assert(custom.Settings.Columns == 5 && custom.Settings.Opacity == .5 && custom.Settings.Theme == "Dark");
        });
        test("Miniatur-Deckkraft und Farbwerte werden begrenzt", () =>
        {
            var group = new BubbleGroup { Color = "#ABCDEF", Opacity = 1 }; var state = new BubbleState { Groups = [group] }; state.Settings.Opacity = 1; state.Settings.AppColor = "bad"; StateStore.Validate(state);
            assert(group.Opacity is null && state.Settings.Opacity == .95 && state.Settings.AppColor == "#7B6BFF" && group.Color == "#ABCDEF");
        });
        test("Alte gemeinsame Farbe bleibt erhalten; Transparenzen bleiben getrennt", () =>
        {
            var group = new BubbleGroup { Color = "#123456", Opacity = .18 };
            var state = new BubbleState { Version = 3, Settings = new BubbleSettings { BubbleColor = "#ABCDEF", Opacity = .27 }, Groups = [group] };
            StateStore.Validate(state);
            assert(state.Version == 6 && state.Settings.WindowColor == "#ABCDEF" && state.Settings.WindowOpacity == .18 && state.Settings.Opacity == .18 && group.WindowColor == "#123456" && group.WindowOpacity is null);
            group.Opacity = .71; group.Color = "#FFFFFF"; StateStore.Validate(state);
            assert(group.WindowOpacity is null && group.Opacity is null && state.Settings.Opacity == .18 && group.WindowColor == "#FFFFFF");
        });
        test("Fenstergröße und Rundungen werden gegen nicht endliche und extreme Werte begrenzt", () =>
        {
            var settings = new BubbleSettings { CornerRoundness = double.NaN, AppCornerRadius = 900, BubbleCornerRadius = -10, WindowOpacity = double.PositiveInfinity, TileSize = 999 };
            var state = new BubbleState { Settings = settings }; StateStore.Validate(state);
            assert(settings.CornerRoundness == 14 && settings.AppCornerRadius == 8 && Math.Abs(settings.BubbleCornerRadius - 10.64) < .001 && settings.WindowOpacity == .35 && settings.TileSize == 160);
        });
        test("Gemeinsame Farbe und Rundung sowie getrennte Transparenzen überstehen Speicher-Roundtrip", () =>
        {
            string dir = Path.Combine(fixtureRoot, "appearance-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "groups.json");
            try
            {
                var state = new BubbleState { Settings = new BubbleSettings { AppOpacity = .9, Opacity = .2, WindowOpacity = .6, AppCornerRadius = 35, BubbleCornerRadius = 12, CornerRoundness = 18, TileSize = 144 }, Groups = [new BubbleGroup { Color = "#00FF00", WindowColor = "#FF0000", Opacity = .24, WindowOpacity = .79 }] };
                new StateStore(path).Save(state); var loaded = StateStore.Read(path);
                assert(loaded.Settings.AppOpacity == .9 && loaded.Settings.Opacity == .2 && loaded.Settings.WindowOpacity == .6 && loaded.Settings.CornerRoundness == 18 && loaded.Settings.TileSize == 144);
                assert(loaded.Groups[0].Color == "#00FF00" && loaded.Groups[0].WindowColor == "#00FF00" && loaded.Groups[0].Opacity is null && loaded.Groups[0].WindowOpacity is null);
            }
            finally { File.Delete(path); Directory.Delete(dir); }
        });
        test("100 Prozent Ordnertransparenz bleibt beim Speichern exakt null Deckkraft", () =>
        {
            var state = new BubbleState { Settings = new BubbleSettings { WindowOpacity = 0 }, Groups = [new BubbleGroup { WindowOpacity = 0 }] };
            StateStore.Validate(state); assert(state.Settings.WindowOpacity == 0 && state.Groups[0].WindowOpacity is null);
            state.Settings.WindowOpacity = -3; state.Groups[0].WindowOpacity = -1; StateStore.Validate(state);
            assert(state.Settings.WindowOpacity == 0 && state.Groups[0].WindowOpacity is null);
        });
        test("Schema 4 vereinheitlicht Farbe und übernimmt Ordner-Rundung proportional", () =>
        {
            var state = new BubbleState { Version = 4, Settings = new BubbleSettings { WindowCornerRadius = 52, TileSize = 116 }, Groups = [new BubbleGroup { Color = "#12AB34", WindowColor = "#DEAD00", Opacity = .2, WindowOpacity = .8 }, new BubbleGroup { WindowColor = "#765432" }] };
            StateStore.Validate(state);
            assert(state.Version == 6 && Math.Abs(state.Settings.CornerRoundness - 52d / 396 * 100) < .0001);
            assert(state.Groups[0].WindowColor == "#12AB34" && state.Groups[1].Color == "#765432" && state.Settings.Opacity == .2 && state.Settings.WindowOpacity == .8 && state.Groups.All(g => g.Opacity is null && g.WindowOpacity is null));
            state.Settings.CornerRoundness = 25; StateStore.Validate(state);
            assert(state.Settings.BubbleCornerRadius == 19 && state.Settings.WindowCornerRadius == 99);
        });
        test("Miniatur erlaubt 100 Prozent Transparenz unabhängig vom Ordner", () =>
        {
            var state = new BubbleState { Settings = new BubbleSettings { Opacity = 0, WindowOpacity = .6 }, Groups = [new BubbleGroup { Opacity = 0, WindowOpacity = .9 }] };
            StateStore.Validate(state); assert(state.Settings.Opacity == 0 && state.Groups[0].Opacity is null && state.Settings.WindowOpacity == .6 && state.Groups[0].WindowOpacity is null);
        });
        test("Schema 5 übernimmt die Transparenzen der ersten Bubble einmalig für alle", () =>
        {
            var state = new BubbleState { Version = 5, Groups = [new BubbleGroup { Opacity = .21, WindowOpacity = .73 }, new BubbleGroup { Opacity = .9, WindowOpacity = .1 }] };
            StateStore.Validate(state);
            assert(state.Version == 6 && state.Settings.Opacity == .21 && state.Settings.WindowOpacity == .73 && state.Groups.All(g => g.Opacity is null && g.WindowOpacity is null));
            state.Settings.Opacity = 0; state.Settings.WindowOpacity = .4; StateStore.Validate(state);
            assert(state.Settings.Opacity == 0 && state.Settings.WindowOpacity == .4 && !state.Settings.LinkBubbleColors && state.Settings.MiniatureSize == 76);
        });
        test("Farbkopplung übernimmt die gewählte Bubble und trennt App-Farbe", () =>
        {
            var first = new BubbleGroup { Color = "#123456" }; var second = new BubbleGroup { Color = "#987654" };
            var state = new BubbleState { Settings = new BubbleSettings { AppColor = "#112233" }, Groups = [first, second] };
            AppearancePreferences.LinkColors(state, second, true);
            AppearancePreferences.SetColor(state, first, "#abcdef");
            assert(state.Settings.LinkBubbleColors && state.Settings.AppColor == "#112233" && state.Groups.All(g => AppearancePreferences.Color(state.Settings, g) == "#ABCDEF"));
            var added = new BubbleGroup(); state.Groups.Add(added);
            assert(AppearancePreferences.Color(state.Settings, added) == "#ABCDEF");
            AppearancePreferences.LinkColors(state, first, false); AppearancePreferences.SetColor(state, first, "#FFFFFF");
            assert(AppearancePreferences.Color(state.Settings, first) == "#FFFFFF" && AppearancePreferences.Color(state.Settings, second) == "#ABCDEF");
        });
        test("Farben zurücksetzen erhält Größen, Transparenzen und Kopplungswahl", () =>
        {
            var state = new BubbleState { Settings = new BubbleSettings { LinkBubbleColors = true, AppColor = "#000000", BubbleColor = "#FFFFFF", Opacity = .17, WindowOpacity = .82, MiniatureSize = 112, TileSize = 144 }, Groups = [new BubbleGroup { Color = "#AABBCC" }] };
            AppearancePreferences.ResetColors(state);
            assert(state.Settings.AppColor == "#7B6BFF" && state.Settings.BubbleColor == "#7B6BFF" && state.Groups[0].Color is null && state.Settings.LinkBubbleColors && state.Settings.MiniatureSize == 112 && state.Settings.TileSize == 144 && state.Settings.Opacity == .17 && state.Settings.WindowOpacity == .82);
        });
        test("Miniaturgröße wird begrenzt und mit Farbkopplung gespeichert", () =>
        {
            var state = new BubbleState { Settings = new BubbleSettings { MiniatureSize = double.NaN, LinkBubbleColors = true } };
            StateStore.Validate(state); assert(state.Settings.MiniatureSize == 76);
            state.Settings.MiniatureSize = 999; StateStore.Validate(state); assert(state.Settings.MiniatureSize == 144);
            state.Settings.MiniatureSize = -1; StateStore.Validate(state); assert(state.Settings.MiniatureSize == 48);
            string path = Path.Combine(fixtureRoot, "miniature-preferences.json");
            try { new StateStore(path).Save(state); var loaded = StateStore.Read(path); assert(loaded.Settings.MiniatureSize == 48 && loaded.Settings.LinkBubbleColors && loaded.Version == 6); }
            finally { File.Delete(path); }
        });
        test("Live-Vorschau misst echte Rastergeometrie und begrenzt lange Ordner", () =>
        {
            var settings = new BubbleSettings(); var four = AppearanceGeometry.Folder(settings, 4); var seven = AppearanceGeometry.Folder(settings, 7);
            assert(four.Width == 396 && four.Height == 278 && seven.Height == 394 && AppearanceGeometry.Folder(settings, 0).Height == 278);
            settings.TileSize = 160; var larger = AppearanceGeometry.Folder(settings, 7);
            assert(larger.Width == 528 && larger.Height == 526 && AppearanceGeometry.Folder(settings, 300).Height == 640);
        });
        test("Gleiche Rundungsquote bleibt bei verschiedenen Größen proportional", () =>
        {
            assert(AppearanceGeometry.Radius(25, 76, 76) == 19 && AppearanceGeometry.Radius(25, 400, 300) == 75 && AppearanceGeometry.Radius(100, 400, 300) == 150);
        });
        test("Live-CPU normalisiert auf alle logischen Prozessoren", () =>
        {
            var series = new CpuSeries(4); series.Observe(0, 0, 100); series.Observe(2, 1, 200);
            assert(Math.Abs(series.Mean - 12.5) < .0001 && series.ActiveSeconds == 2 && series.WorkingSetBytes == 200);
        });
        test("Verspätete Timer werden zeitgewichtet statt als gleich lang behandelt", () =>
        {
            var series = new CpuSeries(4); series.Observe(0, 0, 0); series.Observe(1, 1, 0); series.Observe(3, 1, 0);
            assert(Math.Abs(series.Mean - 25d / 3) < .0001 && Math.Abs(series.Spread - 11.785113) < .0001);
        });
        test("Pause zählt weder Wandzeit noch CPU zur Messung", () =>
        {
            var series = new CpuSeries(1); series.Observe(0, 0, 0); series.Observe(1, .1, 0); series.Break(); series.Observe(100, 50, 0); series.Observe(101, 50.1, 0);
            assert(series.ActiveSeconds == 2 && series.Samples == 2 && Math.Abs(series.Mean - 10) < .0001);
        });
        test("Messhistorie bleibt begrenzt und ungültige Samples verändern Statistik nicht", () =>
        {
            var series = new CpuSeries(8); series.Observe(0, 0, 0); for (int i = 1; i <= 200; i++) series.Observe(i, i * .1, 500);
            double mean = series.Mean; assert(!series.Observe(double.NaN, 100, 0) && !series.Observe(200, 20, 0) && series.History.Count == 90 && series.Samples == 200 && series.Mean == mean);
        });
    }
}
