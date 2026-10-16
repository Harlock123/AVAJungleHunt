using AVAJungleHunt.Engine;

namespace AVAJungleHunt.Tests;

public class GameplayTests
{
    private static string TempFolder() => Path.Combine(Path.GetTempPath(), "avajunglehunt-tests-" + Guid.NewGuid().ToString("N"));

    private static GameEngine Game(Scene scene, int round = 1, bool calm = true, int seed = 7)
    {
        var g = new GameEngine(seed, HighScoreTable.Load(TempFolder())) { Silent = true, Calm = calm };
        g.Go(scene, round);
        return g;
    }

    private static readonly Controls JumpButton = new(Jump: true), Knife = new(Fire: true);

    // ───────────────────────────── the vines ─────────────────────────────

    [Fact]
    public void Every_vine_can_be_reached_from_the_one_before_in_every_round()
    {
        for (int round = 1; round <= 6; round++)
        {
            var g = Game(Scene.Vines, round);
            for (int i = 0; i < g.Vines.Count - 1; i++) Assert.True(GameEngine.Reachable(g.Vines[i], g.Vines[i + 1]), $"round {round}, vine {i}");
            Assert.True(g.Vines.Count >= 10);
        }
    }

    [Fact]
    public void A_vine_swings_like_a_pendulum_and_he_swings_with_it()
    {
        var g = Game(Scene.Vines);
        var v = g.Vines[0];
        Assert.Equal(Math.Sqrt(Field.Gravity / v.Length), v.Rate, 6);
        g.Hang(0, 200);
        double x0 = g.Player.X;
        g.Idle((int)(Math.PI / v.Rate * 60 / 2));
        Assert.NotEqual(x0, g.Player.X, 0);
        var (hx, hy) = v.At(200, g.T);
        Assert.Equal(hx, g.Player.X, 1);
        Assert.Equal(hy + Explorer.Height, g.Player.Y, 1);
    }

    [Fact]
    public void Letting_go_at_the_right_moment_catches_the_next_vine_and_the_wrong_one_drops_him_in_the_swamp()
    {
        var g = Game(Scene.Vines);
        g.SetLives(3);
        // Find a moment the release catches vine 1.
        double good = -1, bad = -1;
        for (double t = 0; t < 12 && (good < 0 || bad < 0); t += 1 / 60.0)
        {
            var p = new Explorer { Along = g.Vines[0].Length - 24 };
            GameEngine.HangOn(p, g.Vines[0], t);
            GameEngine.LetGo(p);
            double tt = t;
            int? got = null;
            for (int k = 0; k < 120 && p.Y < Field.Swamp; k++) { tt += Field.Step; if (GameEngine.Fly(p, g.Vines, 0, tt, Field.Step) is { } c) { got = c.Vine; break; } }
            if (got == 1 && good < 0) good = t;
            if (got == null && bad < 0) bad = t;
        }
        Assert.True(good >= 0 && bad >= 0);
        g.SetT(good - Field.Step);
        g.Hang(0, g.Vines[0].Length - 24);
        g.Run(1, JumpButton);
        g.Idle(90);
        Assert.Equal(1, g.Player.Vine);
        Assert.Equal(GameEngine.VinePoints, g.Score);
        var h = Game(Scene.Vines);
        h.SetLives(3);
        h.SetT(bad - Field.Step);
        h.Hang(0, h.Vines[0].Length - 24);
        h.Run(1, JumpButton);
        h.Idle(120);
        Assert.Equal("swamp", h.Cause);
        Assert.Equal(2, h.Lives);
        h.Idle((int)(GameEngine.DyingSeconds * 60) + 2);
        Assert.Equal(0, h.Player.Vine);
    }

    [Fact]
    public void A_monkey_on_the_vine_knocks_him_off()
    {
        var g = Game(Scene.Vines, 3);
        int i = g.Vines.FindIndex(v => v.Monkey);
        Assert.True(i >= 0);
        var v = g.Vines[i];
        g.Hang(i, v.MonkeyAt(g.T));
        g.Idle(1);
        Assert.Equal("monkey", g.Cause);
    }

    [Fact]
    public void The_last_vine_lets_go_onto_the_far_bank_and_the_scene_is_done()
    {
        var g = Game(Scene.Vines);
        g.PutPlayer(g.Ledge.X0 + 40, Field.LedgeTop - 30);
        g.Idle(30);
        Assert.Equal(Phase.Clear, g.Phase);
        g.Idle((int)(GameEngine.ClearSeconds * 60) + 2);
        Assert.Equal(Scene.River, g.Scene);
    }

    // ───────────────────────────── the river ─────────────────────────────

    [Fact]
    public void Underwater_his_air_runs_out_and_at_the_surface_it_comes_back()
    {
        var g = Game(Scene.River);
        g.SetLives(3);
        g.PutPlayer(200, 500);
        g.Idle(60);
        Assert.Equal(100 - GameEngine.AirDrain, g.Player.Air, 0);
        g.Run(120, new Controls(Dy: 1));
        Assert.Equal(100, g.Player.Air);
        g.PutPlayer(200, 500);
        g.Player.Air = 5;
        g.Idle(60);
        Assert.Equal("drowned", g.Cause);
    }

    [Fact]
    public void The_knife_kills_a_crocodile_with_its_jaws_shut_but_not_with_them_open()
    {
        var g = Game(Scene.River);
        g.PutPlayer(200, 500);
        var k = new Croc { X = 200 + GameEngine.KnifeReach + 20, Y = 500, Cycle = 2, Open = 1, T = 1.5 };
        g.Crocs.Add(k);
        g.Player.Safe = 10;
        g.Run(1, Knife);
        g.Idle(2);
        Assert.True(k.Dead);
        Assert.Equal(GameEngine.CrocPoints, g.Score);
        var h = Game(Scene.River);
        h.PutPlayer(200, 500);
        var o = new Croc { X = 200 + GameEngine.KnifeReach + 20, Y = 500, Cycle = 2, Open = 1, T = 0.2 };
        h.Crocs.Add(o);
        h.Player.Safe = 10;
        h.Run(1, Knife);
        h.Idle(2);
        Assert.False(o.Dead);
    }

    [Fact]
    public void Touching_a_crocodile_is_the_end_and_its_open_mouth_reaches_further()
    {
        var g = Game(Scene.River);
        g.SetLives(3);
        g.PutPlayer(300, 500);
        g.Crocs.Add(new Croc { X = 300 + Croc.Length / 2, Y = 500, Cycle = 2, Open = 1, T = 1.5 });
        g.Idle(1);
        Assert.Equal("crocodile", g.Cause);
        var h = Game(Scene.River);
        h.PutPlayer(300, 500);
        var open = new Croc { X = 300 + Croc.Length / 2 + 20, Y = 500, Cycle = 2, Open = 1, T = 0.2 };
        h.Crocs.Add(open);
        Assert.False(GameEngine.Touches(open, h.Player));
        h.Idle(1);
        Assert.Equal("crocodile", h.Cause);
    }

    [Fact]
    public void Crocodiles_come_on_as_the_screen_reaches_them_and_the_far_end_of_the_river_ends_the_scene()
    {
        var g = Game(Scene.River, calm: false);
        g.Player.Safe = 1000;
        Assert.DoesNotContain(g.Crocs, k => k.X > Field.Width + 100);
        g.Run(600, new Controls(Dx: 1));
        Assert.NotEmpty(g.Crocs);
        Assert.All(g.Crocs, k => Assert.True(k.X < g.Camera + Field.Width + 80));
        g.PutPlayer(g.SceneLength - 50, Field.Surface + 8);
        g.Player.Safe = 1000;
        g.Idle(2);
        Assert.Equal(Phase.Clear, g.Phase);
    }

    // ───────────────────────────── the hill ─────────────────────────────

    [Fact]
    public void On_the_hill_he_runs_up_the_slope_jumps_and_ducks()
    {
        var g = Game(Scene.Hill);
        g.PutPlayer(200, Field.HillGround(200));
        g.Run(60, new Controls(Dx: 1));
        Assert.Equal(200 + GameEngine.RunSpeed, g.Player.X, 0);
        Assert.Equal(Field.HillGround(g.Player.X), g.Player.Y, 3);
        g.Run(1, JumpButton);
        g.Idle(10);
        Assert.False(g.Player.Grounded);
        g.Idle(60);
        Assert.True(g.Player.Grounded);
        g.Run(1, new Controls(Dy: -1));
        Assert.True(g.Player.Ducking);
        Assert.Equal(Explorer.DuckHeight, g.Player.Tall);
    }

    [Fact]
    public void A_small_boulder_is_jumped_a_big_one_ducked_under_and_one_that_hits_is_the_end()
    {
        // Small: standing still it hits him, but there's a moment to jump that clears it.
        bool Small(int jumpAt)
        {
            var b = new Boulder { X = 500, Y = Field.HillGround(500) - 22, Vx = -250, R = 22 };
            var me = new Explorer { X = 300, Y = Field.HillGround(300), Grounded = true };
            bool hit = false;
            for (int i = 0; i < 120; i++)
            {
                GameEngine.Run(me, new Controls(Jump: i == jumpAt), Field.HillGround, 0, 2400, Field.Step);
                GameEngine.Roll(b, Field.Step);
                hit |= GameEngine.Hits(b, me);
            }
            return hit;
        }
        Assert.True(Small(-1));
        Assert.Contains(Enumerable.Range(0, 60), i => !Small(i));
        // Big: there are times standing gets him hit where ducking lets it bounce over.
        bool ducked = false;
        for (double gap = 150; gap < 600 && !ducked; gap += 10)
        {
            var b = new Boulder { X = 300 + gap, Y = Field.HillGround(300 + gap) - 42, Vx = -200, R = 42, Big = true };
            var d = new Explorer { X = 300, Y = Field.HillGround(300), Grounded = true };
            var s = new Explorer { X = 300, Y = Field.HillGround(300), Grounded = true };
            var b2 = new Boulder { X = b.X, Y = b.Y, Vx = b.Vx, R = b.R, Big = true };
            bool dh = false, sh = false;
            for (int i = 0; i < 180; i++)
            {
                GameEngine.Run(d, new Controls(Dy: -1), Field.HillGround, 0, 2400, Field.Step);
                GameEngine.Run(s, default, Field.HillGround, 0, 2400, Field.Step);
                GameEngine.Roll(b, Field.Step);
                GameEngine.Roll(b2, Field.Step);
                dh |= GameEngine.Hits(b, d);
                sh |= GameEngine.Hits(b2, s);
            }
            ducked = sh && !dh;
        }
        Assert.True(ducked);
        var g = Game(Scene.Hill);
        g.SetLives(3);
        g.PutPlayer(300, Field.HillGround(300));
        g.Boulders.Add(new Boulder { X = 300, Y = Field.HillGround(300) - 30, Vx = -100, R = 22 });
        g.Idle(1);
        Assert.Equal("boulder", g.Cause);
    }

    [Fact]
    public void Boulders_keep_coming_down_the_hill_and_getting_past_one_scores()
    {
        var g = Game(Scene.Hill, calm: false);
        g.Player.Safe = 1000;
        g.Idle(60 * 6);
        Assert.NotEmpty(g.Boulders);
        Assert.True(g.Score > 0);
        Assert.All(g.Boulders, b => Assert.True(b.Vx < 0));
    }

    // ───────────────────────────── the camp ─────────────────────────────

    [Fact]
    public void A_poacher_hops_in_the_way_and_jumping_him_scores_while_touching_him_is_the_end()
    {
        var g = Game(Scene.Camp, calm: false);
        var gd = g.Guards[0];
        var (x, y) = gd.At(g.T);
        Assert.InRange(x, gd.Home - gd.Pace - 1, gd.Home + gd.Pace + 1);
        Assert.True(Enumerable.Range(0, 120).Select(i => gd.At(i / 60.0).Y).Min() < Field.CampGround - gd.Hop * 0.9);
        g.SetLives(3);
        // A moment it's down on the ground.
        double down = Enumerable.Range(0, 200).Select(i => i / 60.0).First(t => gd.At(t + Field.Step).Y >= Field.CampGround - 1);
        g.SetT(down);
        g.PutPlayer(gd.At(down + Field.Step).X, Field.CampGround);
        g.Idle(1);
        Assert.Equal("caught", g.Cause);
        // Over one, with a clean jump.
        var h = Game(Scene.Camp);
        // (Held still, and on the ground.)
        h.Guards[0].Phase = 0;
        h.PutPlayer(h.Guards[0].At(0).X - 80, Field.CampGround);
        h.SetT(0);
        h.Run(1, new Controls(Dx: 1, Jump: true));
        h.Run(80, new Controls(Dx: 1));
        Assert.True(h.Player.Alive);
        Assert.True(h.Player.X > h.Guards[0].Home);
        Assert.Equal(GameEngine.GuardPoints, h.Score);
    }

    [Fact]
    public void Reaching_the_photographer_rescues_him_and_the_next_round_begins_with_the_vines()
    {
        var g = Game(Scene.Camp);
        g.PutPlayer(g.CaptiveX - 20, Field.CampGround);
        g.Idle(2);
        Assert.Equal(Phase.Clear, g.Phase);
        Assert.Equal(GameEngine.RescueBonus, g.Bonus);
        int score = g.Score;
        g.Idle((int)(GameEngine.ClearSeconds * 60) + 2);
        Assert.Equal(score + GameEngine.RescueBonus, g.Score);
        Assert.Equal((2, Scene.Vines), (g.Wave, g.Scene));
    }

    [Fact]
    public void Each_round_is_harder()
    {
        var one = Game(Scene.Vines, 1);
        var four = Game(Scene.Vines, 4);
        Assert.True(four.Vines.Count > one.Vines.Count);
        Assert.Contains(four.Vines, v => v.Monkey);
        Assert.DoesNotContain(one.Vines, v => v.Monkey);
        Assert.True(Game(Scene.Camp, 4).Guards.Count > Game(Scene.Camp, 1).Guards.Count);
        Assert.True(Game(Scene.Camp, 4).Guards.Max(g => g.Hop) > Game(Scene.Camp, 1).Guards.Max(g => g.Hop));
    }

    // ───────────────────────────── the whole game ─────────────────────────────

    [Fact]
    public void An_extra_life_every_20000()
    {
        var g = Game(Scene.Camp);
        int lives = g.Lives;
        g.SetScore(GameEngine.ExtraLifeEvery - 50);
        g.PutPlayer(g.CaptiveX - 20, Field.CampGround);
        g.Idle(90);
        Assert.Equal(lives + 1, g.Lives);
    }

    [Fact]
    public void The_last_life_ends_the_game_and_a_high_score_asks_for_initials()
    {
        var g = Game(Scene.Hill);
        g.SetLives(1);
        g.SetScore(99999);
        g.PutPlayer(300, Field.HillGround(300));
        g.Boulders.Add(new Boulder { X = 300, Y = Field.HillGround(300) - 30, Vx = -100, R = 22 });
        g.Idle(1);
        g.Idle((int)(GameEngine.DyingSeconds * 60) + 2);
        Assert.Equal(GameState.GameOver, g.State);
        g.Idle(60 * 4);
        Assert.Equal(GameState.EnterInitials, g.State);
        foreach (char ch in "VIN") g.Initials!.Type(ch);
        g.Initials!.Finish();
        g.Idle(1);
        Assert.Equal(GameState.HighScores, g.State);
        Assert.Equal(("VIN", 99999), (g.HighScores.Entries[0].Initials, g.HighScores.Entries[0].Score));
    }

    [Fact]
    public void The_title_runs_a_demonstration_of_each_scene_in_turn_then_the_high_scores()
    {
        var g = new GameEngine(5, HighScoreTable.Load(TempFolder())) { Silent = true };
        var scenes = new List<Scene>();
        for (int round = 0; round < 2; round++)
        {
            g.Idle((int)(GameEngine.TitleIdleSeconds * 60) + 2);
            Assert.Equal(GameState.Attract, g.State);
            scenes.Add(g.Scene);
            for (int i = 0; i < (int)(GameEngine.AttractSeconds * 60) + 2 && g.State == GameState.Attract; i++) g.Idle(1);
            Assert.Equal(GameState.HighScores, g.State);
            g.Idle((int)(GameEngine.HighScoreSeconds * 60) + 2);
            Assert.Equal(GameState.Title, g.State);
        }
        Assert.Equal([Scene.Vines, Scene.River], scenes);
    }

    [Fact]
    public void Enter_starts_a_game_on_the_vines()
    {
        var g = new GameEngine(5, HighScoreTable.Load(TempFolder())) { Silent = true };
        g.Tick(default, start: true);
        Assert.Equal(GameState.Playing, g.State);
        Assert.Equal((1, Scene.Vines, 3), (g.Wave, g.Scene, g.Lives));
        Assert.Equal(0, g.Player.Vine);
    }

    [Fact]
    public void The_computer_player_gets_through_a_whole_round()
    {
        var g = new GameEngine(7, HighScoreTable.Load(TempFolder())) { Silent = true };
        g.StartGame();
        g.SetLives(99);
        for (int i = 0; i < 60 * 200 && g.Wave < 2 && g.State == GameState.Playing; i++) g.Tick(g.Pilot.Decide(g));
        Assert.True(g.Wave >= 2, $"still in round {g.Wave}, {g.Scene}");
    }

    [Fact]
    public void The_same_seed_plays_the_same_game()
    {
        string Play()
        {
            var g = new GameEngine(3, HighScoreTable.Load(TempFolder())) { Silent = true };
            g.StartGame();
            for (int i = 0; i < 60 * 60; i++) g.Tick(g.Pilot.Decide(g));
            return $"{g.Score} {g.Wave} {g.Scene} {g.Lives} {g.Player.X:0.00} {g.Player.Y:0.00} {g.Phase}";
        }
        Assert.Equal(Play(), Play());
    }

    [Fact]
    public void Pausing_stops_everything()
    {
        var g = Game(Scene.Hill, calm: false);
        g.Run(30, new Controls(Dx: 1));
        double x = g.Player.X;
        g.Pause();
        g.Idle(120);
        Assert.Equal(x, g.Player.X);
        g.Resume();
        Assert.Equal(GameState.Playing, g.State);
    }
}
