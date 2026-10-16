using AVAJungleHunt.Engine;

namespace AVAJungleHunt.Tests;

/// <summary>How the computer player gets on, scene by scene (set JUNGLEHUNT_PROBE to run it, for tuning: "1" for the pilot, "lazy" for one that just runs right jumping and stabbing).</summary>
public sealed class PilotProbe(ITestOutputHelper output)
{
    [Fact]
    public void How_the_computer_plays()
    {
        string? mode = Environment.GetEnvironmentVariable("JUNGLEHUNT_PROBE");
        if (mode is null) return;
        foreach (int seed in new[] { 1, 2, 3 })
        {
            var g = new GameEngine(seed, HighScoreTable.Load(Path.Combine(Path.GetTempPath(), "junglehunt-probe-" + Guid.NewGuid().ToString("N")))) { Silent = true };
            g.StartGame();
            g.SetLives(99);
            var deaths = new Dictionary<string, int>();
            var scenes = new List<string>();
            var doing = new Dictionary<string, int>();
            var last = g.Phase;
            var at = (g.Wave, g.Scene);
            int t = 0;
            double start = 0;
            for (; t < 60 * 900 && g.Wave <= 4 && g.State == GameState.Playing; t++)
            {
                var c = mode == "lazy" ? new Controls(Dx: 1, Jump: t % 40 == 0, Fire: t % 20 == 0) : g.Pilot.Decide(g);
                g.Tick(c);
                if (g.Phase == Phase.Play) doing[$"{g.Scene} {g.Pilot.Doing}"] = doing.GetValueOrDefault($"{g.Scene} {g.Pilot.Doing}") + 1;
                if (g.Phase == Phase.Dying && last != Phase.Dying) deaths[$"R{g.Wave} {g.Cause}"] = deaths.GetValueOrDefault($"R{g.Wave} {g.Cause}") + 1;
                if ((g.Wave, g.Scene) != at) { scenes.Add($"R{at.Item1}{at.Item2.ToString()[0]}:{(t - start) / 60:0}s"); at = (g.Wave, g.Scene); start = t; }
                last = g.Phase;
            }
            output.WriteLine($"seed {seed}: round {g.Wave} {g.Scene} after {t / 60:0}s score {g.Score} | {string.Join(" ", scenes)}");
            output.WriteLine("   deaths " + string.Join(", ", deaths.OrderBy(d => d.Key).Select(d => $"{d.Key} x{d.Value}")));
            output.WriteLine("   doing " + string.Join(", ", doing.OrderByDescending(d => d.Value).Take(12).Select(d => $"{d.Key} {d.Value / 60}s")));
        }
    }

    /// <summary>The lazy player dropped into each scene of a round in turn: how often it dies there, and whether it gets through.</summary>
    [Fact]
    public void How_the_lazy_one_does_in_each_scene()
    {
        if (Environment.GetEnvironmentVariable("JUNGLEHUNT_PROBE") != "scenes") return;
        foreach (int round in new[] { 1, 3 })
        foreach (var scene in Enum.GetValues<Scene>())
        {
            var g = new GameEngine(1, HighScoreTable.Load(Path.Combine(Path.GetTempPath(), "junglehunt-probe-" + Guid.NewGuid().ToString("N")))) { Silent = true };
            g.Go(scene, round);
            g.SetLives(99);
            int deaths = 0, t = 0;
            var last = g.Phase;
            for (; t < 60 * 120 && g.Scene == scene && g.Wave == round; t++)
            {
                g.Tick(new Controls(Dx: 1, Jump: t % 40 == 0, Fire: t % 20 == 0));
                if (g.Phase == Phase.Dying && last != Phase.Dying) deaths++;
                last = g.Phase;
            }
            output.WriteLine($"round {round} {scene}: {(g.Scene == scene ? "stuck" : "through")} after {t / 60}s, {deaths} deaths");
        }
    }
}
