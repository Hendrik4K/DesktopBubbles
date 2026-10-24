using DesktopBubbles.Core;

internal static class AppearanceTests
{
    public static void Run(Action<string, Action> test, Action<bool> assert, string fixtureRoot)
    {
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
            assert(state.Version == 5 && state.Settings.Columns == 3 && state.Settings.Opacity == .35 && state.Settings.Theme == "Light");
            var custom = new BubbleState { Version = 2, Settings = new BubbleSettings { Theme = "Dark", Columns = 5, Opacity = .5 } }; StateStore.Validate(custom);
            assert(custom.Settings.Columns == 5 && custom.Settings.Opacity == .5 && custom.Settings.Theme == "Dark");
        });
        test("Miniatur-Deckkraft und Farbwerte werden begrenzt", () =>
        {
            var group = new BubbleGroup { Color = "#ABCDEF", Opacity = 1 }; var state = new BubbleState { Groups = [group] }; state.Settings.Opacity = 1; state.Settings.AppColor = "bad"; StateStore.Validate(state);
            assert(group.Opacity == .95 && state.Settings.Opacity == .95 && state.Settings.AppColor == "#7B6BFF" && group.Color == "#ABCDEF");
        });
        test("Alte gemeinsame Farbe bleibt erhalten; Transparenzen bleiben getrennt", () =>
        {
            var group = new BubbleGroup { Color = "#123456", Opacity = .18 };
            var state = new BubbleState { Version = 3, Settings = new BubbleSettings { BubbleColor = "#ABCDEF", Opacity = .27 }, Groups = [group] };
            StateStore.Validate(state);
            assert(state.Version == 5 && state.Settings.WindowColor == "#ABCDEF" && state.Settings.WindowOpacity == .27 && group.WindowColor == "#123456" && group.WindowOpacity == .18);
            group.Opacity = .71; group.Color = "#FFFFFF"; StateStore.Validate(state);
            assert(group.WindowOpacity == .18 && group.WindowColor == "#FFFFFF");
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
                assert(loaded.Groups[0].Color == "#00FF00" && loaded.Groups[0].WindowColor == "#00FF00" && loaded.Groups[0].Opacity == .24 && loaded.Groups[0].WindowOpacity == .79);
            }
            finally { File.Delete(path); Directory.Delete(dir); }
        });
        test("100 Prozent Ordnertransparenz bleibt beim Speichern exakt null Deckkraft", () =>
        {
            var state = new BubbleState { Settings = new BubbleSettings { WindowOpacity = 0 }, Groups = [new BubbleGroup { WindowOpacity = 0 }] };
            StateStore.Validate(state); assert(state.Settings.WindowOpacity == 0 && state.Groups[0].WindowOpacity == 0);
            state.Settings.WindowOpacity = -3; state.Groups[0].WindowOpacity = -1; StateStore.Validate(state);
            assert(state.Settings.WindowOpacity == 0 && state.Groups[0].WindowOpacity == 0);
        });
        test("Schema 4 vereinheitlicht Farbe und übernimmt Ordner-Rundung proportional", () =>
        {
            var state = new BubbleState { Version = 4, Settings = new BubbleSettings { WindowCornerRadius = 52, TileSize = 116 }, Groups = [new BubbleGroup { Color = "#12AB34", WindowColor = "#DEAD00", Opacity = .2, WindowOpacity = .8 }, new BubbleGroup { WindowColor = "#765432" }] };
            StateStore.Validate(state);
            assert(state.Version == 5 && Math.Abs(state.Settings.CornerRoundness - 52d / 396 * 100) < .0001);
            assert(state.Groups[0].WindowColor == "#12AB34" && state.Groups[1].Color == "#765432" && state.Groups[0].Opacity == .2 && state.Groups[0].WindowOpacity == .8);
            state.Settings.CornerRoundness = 25; StateStore.Validate(state);
            assert(state.Settings.BubbleCornerRadius == 19 && state.Settings.WindowCornerRadius == 99);
        });
        test("Miniatur erlaubt 100 Prozent Transparenz unabhängig vom Ordner", () =>
        {
            var state = new BubbleState { Settings = new BubbleSettings { Opacity = 0, WindowOpacity = .6 }, Groups = [new BubbleGroup { Opacity = 0, WindowOpacity = .9 }] };
            StateStore.Validate(state); assert(state.Settings.Opacity == 0 && state.Groups[0].Opacity == 0 && state.Groups[0].WindowOpacity == .9);
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
