using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVAJungleHunt.Engine;
using AVAJungleHunt.Views;
using Controls = AVAJungleHunt.Engine.Controls;

namespace AVAJungleHunt.Tests;

/// <summary>
/// Draws the game in a real (headless) window at the moments worth showing. Set JUNGLEHUNT_SHOTS to a folder to
/// save them as PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests
{
    private static (Window W, GameCanvas Canvas, GameEngine G) Open(int seed = 11)
    {
        var g = new GameEngine(seed, HighScoreTable.Load(Path.Combine(Path.GetTempPath(), "avajunglehunt-ui-" + Guid.NewGuid().ToString("N")))) { Silent = true };
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.InvalidateVisual();
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("JUNGLEHUNT_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVAJungleHunt-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    /// <summary>Lets the computer pilot play until something worth a picture is on screen.</summary>
    private static bool PlayUntil(GameEngine g, GameCanvas c, Func<GameEngine, bool> done, int maxSeconds = 150, bool holdFire = false)
    {
        for (int t = 0; t < 60 * maxSeconds; t++)
        {
            if (done(g)) { c.Advance(0.016); return true; }
            var move = g.Pilot.Decide(g);
            g.Tick(holdFire ? move with { Jump = false, Fire = false } : move);
            if (t % 3 == 0) c.Advance(1 / 20.0);
        }
        return false;
    }
    [AvaloniaFact]
    public void The_title_and_the_high_scores()
    {
        var (w, c, g) = Open();
        g.Idle(30);
        c.Advance(2.1);
        Shot(w, c, "title");
        g.HighScores.Insert("ACE", 96400, 5);
        g.HighScores.Insert("AVA", 61200, 3);
        g.Idle((int)(60 * (GameEngine.TitleIdleSeconds + GameEngine.AttractSeconds)) + 10);
        for (int i = 0; i < 60 * 30 && g.State != GameState.HighScores; i++) g.Idle(1);
        Shot(w, c, "high-scores");
    }

    /// <summary>A scene, and the computer at work in it until something worth showing.</summary>
    private static void Scene_(Scene scene, int round, string name, Func<GameEngine, bool> busy, int seed = 3, int seconds = 90)
    {
        var (w, c, g) = Open(seed);
        g.Go(scene, round);
        g.SetLives(30);
        Assert.True(PlayUntil(g, c, g => g.Phase == Phase.Play && g.Player.Alive && g.Player.Safe <= 0 && busy(g), seconds));
        Shot(w, c, name);
    }

    [AvaloniaFact]
    public void The_vines() => Scene_(Scene.Vines, 2, "vines", g => g.Player.Vine < 0 && g.Player.Vy < 0 && g.Vines.Any(v => v.Monkey && v.X > g.Camera + 200 && v.X < g.Camera + 900));

    [AvaloniaFact]
    public void The_river() => Scene_(Scene.River, 1, "river", g => g.Crocs.Count(k => !k.Dead && k.X > g.Camera + 100 && k.X < g.Camera + 950) >= 2 && g.Crocs.Any(k => k.JawsOpen && k.X > g.Camera + 100 && k.X < g.Camera + 950) && g.Player.Y > Field.Surface + 30);

    [AvaloniaFact]
    public void The_hill() => Scene_(Scene.Hill, 2, "hill", g => g.Boulders.Any(b => b.Big && b.X > g.Player.X && b.X - g.Player.X < 220) && g.Boulders.Count >= 2);

    [AvaloniaFact]
    public void The_camp() => Scene_(Scene.Camp, 2, "camp", g => !g.Player.Grounded && g.Guards.Any(gd => Math.Abs(gd.At(g.T).X - g.Player.X) < 50));

    [AvaloniaFact]
    public void The_rescue()
    {
        var (w, c, g) = Open(4);
        g.Go(Scene.Camp, 1);
        g.SetLives(20);
        Assert.True(PlayUntil(g, c, g => g.Phase == Phase.Clear && g.PhaseTime > 0.5, 90));
        Shot(w, c, "rescue");
    }
}
