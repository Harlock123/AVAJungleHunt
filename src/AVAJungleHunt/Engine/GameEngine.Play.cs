namespace AVAJungleHunt.Engine;

public sealed partial class GameEngine
{
    public const double StartSeconds = 2.2, DyingSeconds = 2.4, SafeSeconds = 2, ClearSeconds = 2.4;
    public const int ExtraLifeEvery = 20000, VinePoints = 100, CrocPoints = 200, BoulderPoints = 100, BigBoulderPoints = 200, GuardPoints = 300, SceneBonus = 500, RescueBonus = 5000;

    /// <summary>The round: 1 for the first. Each round is the four scenes in turn, harder each time.</summary>
    public int Wave { get; private set; } = 1;
    public Scene Scene { get; private set; } = Scene.Vines;
    public int Score { get; private set; }
    public int Lives { get; private set; }
    public Explorer Player { get; } = new();
    /// <summary>How far along the scene the left of the screen is.</summary>
    public double Camera { get; private set; }
    public double SceneLength { get; private set; }
    /// <summary>The scene's clock (the vines swing and the guards hop by it).</summary>
    public double T { get; private set; }
    public List<Vine> Vines { get; } = [];
    /// <summary>The far bank at the end of the vines: where it starts and ends.</summary>
    public (double X0, double X1) Ledge { get; private set; }
    public List<Croc> Crocs { get; } = [];
    public List<Boulder> Boulders { get; } = [];
    public List<Guard> Guards { get; } = [];
    public double CaptiveX { get; private set; }
    public ExplorerPilot Pilot { get; } = new();
    public string Cause { get; private set; } = "";
    public int Bonus { get; private set; }
    /// <summary>Where a new life starts in this scene: the last vine caught, or how far along.</summary>
    public int CheckVine { get; private set; }
    public double CheckX { get; private set; }
    /// <summary>For tests: the crocodiles, boulders and guards hold still and nothing new comes.</summary>
    internal bool Calm { get; set; }

    private int _nextExtra, _bestVine, _crocIndex;
    private double _boulderIn;
    private readonly List<Croc> _crocsToCome = [];

    public static string NameOf(Scene s) => s switch
    {
        Scene.Vines => "THE VINES", Scene.River => "THE RIVER", Scene.Hill => "THE HILL", _ => "THE POACHERS' CAMP",
    };

    private void NewGame()
    {
        _rng = new Random(_seed);
        Score = 0;
        Lives = 3;
        Wave = 1;
        _nextExtra = ExtraLifeEvery;
        BuildScene(Scene.Vines);
        SetPhase(Phase.Start);
    }

    private void BeginRound(int round)
    {
        Wave = round;
        BeginScene(Scene.Vines);
    }

    /// <summary>The demonstration: the scenes of the first round in turn, one each time.</summary>
    private void BeginDemo(int n)
    {
        Wave = 1;
        BeginScene((Scene)(n % 4));
    }

    private void BeginScene(Scene scene)
    {
        BuildScene(scene);
        SetPhase(Phase.Start);
        Say(NameOf(scene), Wave > 1 && scene == Scene.Vines ? $"ROUND {Wave}" : null, StartSeconds + 0.3);
        Play(Sfx.SceneStart);
        Music(true);
    }

    /// <summary>A scene laid out: the same for the same round and scene every time, and harder each round.</summary>
    private void BuildScene(Scene scene)
    {
        Scene = scene;
        T = 0;
        Camera = 0;
        Vines.Clear();
        Crocs.Clear();
        Boulders.Clear();
        Guards.Clear();
        _crocsToCome.Clear();
        _crocIndex = 0;
        CheckVine = 0;
        CheckX = 100;
        _bestVine = 0;
        var rng = new Random(1982 + Wave * 131 + (int)scene * 17);
        switch (scene)
        {
            case Scene.Vines:
            {
                int n = 9 + Math.Min(6, Wave);
                double x = 260;
                for (int i = 0; i < n; i++)
                {
                    var v = new Vine
                    {
                        X = x, Length = 290 + rng.NextDouble() * 50, Swing = 0.45 + rng.NextDouble() * 0.15, Phase = rng.NextDouble() * Math.PI * 2,
                        Monkey = Wave >= 2 && i >= 2 && rng.NextDouble() < Math.Min(0.5, 0.2 + 0.08 * Wave), MonkeyRate = 1.2 + rng.NextDouble() * 0.8,
                    };
                    // Pulled in closer until the last vine can be reached from it.
                    while (Vines.Count > 0 && !Reachable(Vines[^1], v) && v.X - Vines[^1].X > 150) v.X -= 8;
                    Vines.Add(v);
                    x = v.X + 205 + rng.NextDouble() * 45;
                }
                double last = Vines[^1].X;
                Ledge = (last + 170, last + 760);
                SceneLength = Ledge.X1;
                break;
            }
            case Scene.River:
            {
                SceneLength = 2800;
                for (double x = 700; x < SceneLength - 200; x += 260 + rng.NextDouble() * 140 - Math.Min(80, Wave * 10))
                {
                    double cycle = Math.Max(1.4, 2.2 - 0.1 * (Wave - 1));
                    _crocsToCome.Add(new Croc
                    {
                        X = x, Y = (rng.Next(2) == 0 ? Field.Surface + 18 : Field.Surface + 40 + rng.NextDouble() * (Field.RiverBed - Field.Surface - 100)), Speed = 50 + rng.NextDouble() * 40 + 8 * (Wave - 1),
                        Cycle = cycle, Open = cycle * Math.Min(0.65, 0.5 + 0.04 * (Wave - 1)), T = rng.NextDouble() * cycle,
                    });
                }
                break;
            }
            case Scene.Hill:
                SceneLength = 2400;
                _boulderIn = 1.5;
                break;
            default:
            {
                int n = Math.Min(4, 2 + (Wave - 1) / 2);
                double x = 420;
                for (int i = 0; i < n; i++)
                {
                    Guards.Add(new Guard
                    {
                        Home = x, Hop = Math.Min(160, 85 + 18 * (Wave - 1)) * (0.8 + rng.NextDouble() * 0.4), Period = Math.Max(0.7, 1.1 - 0.05 * (Wave - 1)) * (0.85 + rng.NextDouble() * 0.3),
                        Phase = rng.NextDouble(), Pace = Math.Min(80, 30 + 10 * (Wave - 1)),
                    });
                    x += 280;
                }
                CaptiveX = x + 80;
                SceneLength = CaptiveX + 140;
                break;
            }
        }
        PlacePlayer();
    }

    /// <summary>The explorer at the scene's last safe place: hanging on the last vine caught, or on his feet (or afloat) a way back.</summary>
    private void PlacePlayer()
    {
        var p = Player;
        p.Alive = true;
        p.Safe = SafeSeconds;
        p.Vx = p.Vy = 0;
        p.Ducking = false;
        p.Air = 100;
        p.Stab = p.StabIn = 0;
        p.Facing = 1;
        switch (Scene)
        {
            case Scene.Vines:
            {
                p.Vine = CheckVine;
                p.Along = Vines[CheckVine].Length - 24;
                p.Grounded = false;
                HangOn(p, Vines[CheckVine], T);
                break;
            }
            case Scene.River:
                p.Vine = -1;
                p.X = CheckX;
                p.Y = Field.Surface + 8;
                p.Grounded = false;
                // Nothing waiting right on top of him.
                Crocs.RemoveAll(c => Math.Abs(c.X - p.X) < 260);
                break;
            case Scene.Hill:
                p.Vine = -1;
                p.X = CheckX;
                p.Y = Field.HillGround(p.X);
                p.Grounded = true;
                Boulders.Clear();
                _boulderIn = 1.5;
                break;
            default:
                p.Vine = -1;
                p.X = CheckX;
                p.Y = Field.CampGround;
                p.Grounded = true;
                break;
        }
        Follow();
    }

    /// <summary>The screen keeps the explorer a way in from the left, never going back or past the scene's end.</summary>
    private void Follow()
    {
        double want = Math.Clamp(Player.X - Field.Lead, 0, Math.Max(0, SceneLength - Field.Width));
        if (want > Camera) Camera = want;
    }

    private void Simulate(double dt, Controls c)
    {
        switch (Phase)
        {
            case Phase.Start:
                T += dt;
                if (Scene == Scene.Vines && Player.Vine >= 0) HangOn(Player, Vines[Player.Vine], T);
                if (PhaseTime >= StartSeconds) SetPhase(Phase.Play);
                break;
            case Phase.Play:
                T += dt;
                Player.Safe = Math.Max(0, Player.Safe - dt);
                switch (Scene)
                {
                    case Scene.Vines: Vines_(dt, c); break;
                    case Scene.River: River(dt, c); break;
                    case Scene.Hill: Hill(dt, c); break;
                    default: Camp(dt, c); break;
                }
                Follow();
                break;
            case Phase.Dying:
                T += dt;
                if (Scene == Scene.Hill) MoveBoulders(dt);
                if (Scene == Scene.River) MoveCrocs(dt);
                if (PhaseTime >= DyingSeconds)
                {
                    if (Lives <= 0) { GameOver(); return; }
                    PlacePlayer();
                    SetPhase(Phase.Play);
                }
                break;
            case Phase.Clear:
                T += dt;
                if (PhaseTime >= 1 && PhaseTime - dt < 1 && Bonus > 0)
                {
                    Score += Bonus;
                    CheckExtra();
                }
                if (PhaseTime >= ClearSeconds)
                {
                    if (Scene == Scene.Camp) BeginRound(Wave + 1);
                    else BeginScene(Scene + 1);
                }
                break;
        }
        PhaseTime += dt;
    }

    // ───────────────────────────── the vines ─────────────────────────────

    /// <summary>The explorer hanging by his hands from a vine, a distance down it.</summary>
    public static void HangOn(Explorer p, Vine v, double t)
    {
        var (x, y) = v.At(p.Along, t);
        p.X = x;
        p.Y = y + Explorer.Height;
        double a = v.AngleAt(t), w = v.SpinAt(t);
        p.Vx = Math.Cos(a) * w * p.Along;
        p.Vy = -Math.Sin(a) * w * p.Along;
    }

    /// <summary>Letting go: off with the vine's swing, and a little spring of his own.</summary>
    public static void LetGo(Explorer p)
    {
        p.Vine = -1;
        p.Vx += 110;
        p.Vy -= 300;
    }

    /// <summary>A step of flight between vines; returns the vine (and how far down it) he catches, if he does.</summary>
    public static (int Vine, double Along)? Fly(Explorer p, IReadOnlyList<Vine> vines, int last, double t, double dt)
    {
        p.Vy += Field.Gravity * dt;
        p.X += p.Vx * dt;
        p.Y += p.Vy * dt;
        double hx = p.X, hy = p.Y - Explorer.Height;
        for (int i = 0; i < vines.Count; i++)
        {
            if (i == last) continue;
            var v = vines[i];
            double a = v.AngleAt(t), ux = Math.Sin(a), uy = Math.Cos(a);
            double along = (hx - v.X) * ux + (hy - Field.Canopy) * uy;
            // Anywhere down it, or just below its tip (he grabs the end).
            if (along < 60 || along > v.Length + 24) continue;
            double off = Math.Abs((hx - v.X) * uy - (hy - Field.Canopy) * ux);
            if (off < 26) return (i, Math.Min(along, v.Length - 8));
        }
        return null;
    }

    /// <summary>Whether, letting go of one vine at some moment in the next while, he'd catch the next (at a few moments, not just one).</summary>
    public static bool Reachable(Vine from, Vine to)
    {
        int ok = 0;
        var pair = new[] { from, to };
        for (double t0 = 0; t0 < 12; t0 += 1 / 30.0)
        {
            var p = new Explorer { Along = from.Length - 24 };
            HangOn(p, from, t0);
            LetGo(p);
            double t = t0;
            for (int k = 0; k < 120 && p.Y < Field.Swamp; k++)
            {
                t += Field.Step;
                if (Fly(p, pair, 0, t, Field.Step) is { } c) { if (c.Vine == 1) ok++; break; }
            }
            if (ok >= 3) return true;
        }
        return false;
    }

    private int _lastVine = -1;

    private void Vines_(double dt, Controls c)
    {
        var p = Player;
        if (p.Vine >= 0)
        {
            var v = Vines[p.Vine];
            HangOn(p, v, T);
            if (v.Monkey && p.Safe <= 0 && Math.Abs(v.MonkeyAt(T) - p.Along) < 28) { Kill("monkey"); return; }
            if (c.Jump)
            {
                _lastVine = p.Vine;
                LetGo(p);
                Play(Sfx.Swing);
            }
            return;
        }
        if (p.Grounded)
        {
            p.X = Math.Min(p.X + 60 * dt, Ledge.X0 + 120);
            return;
        }
        double oldY = p.Y;
        var caught = Fly(p, Vines, _lastVine, T, dt);
        if (caught is { } got)
        {
            p.Vine = got.Vine;
            p.Along = got.Along;
            Play(Sfx.Grab);
            if (got.Vine > _bestVine)
            {
                _bestVine = got.Vine;
                Award(VinePoints, p.X, p.Y - 70);
            }
            CheckVine = got.Vine;
            HangOn(p, Vines[got.Vine], T);
            return;
        }
        // The far bank.
        if (oldY <= Field.LedgeTop && p.Y >= Field.LedgeTop && p.X >= Ledge.X0 && p.X <= Ledge.X1)
        {
            p.Y = Field.LedgeTop;
            p.Vx = p.Vy = 0;
            p.Grounded = true;
            Done(SceneBonus, "ACROSS THE SWAMP");
            return;
        }
        if (p.Y > Field.Swamp) Kill("swamp");
    }

    // ───────────────────────────── the river ─────────────────────────────

    public const double SwimSpeed = 170, DiveSpeed = 150, AirDrain = 11, AirFill = 50, KnifeReach = 30, BiteReach = 40;

    /// <summary>One step of swimming: along and up or down, kept in the water and on the screen.</summary>
    public static void Swim(Explorer p, Controls c, double camera, double dt)
    {
        p.X = Math.Max(camera + 24, p.X + c.Dx * SwimSpeed * dt);
        p.Y = Math.Clamp(p.Y - c.Dy * DiveSpeed * dt, Field.Surface + 8, Field.RiverBed - 22);
        if (c.Dx != 0) p.Facing = c.Dx;
        if (c.Dx != 0 || c.Dy != 0) p.Walk += dt;
    }

    public static bool Under(Explorer p) => p.Y > Field.Surface + 26;

    private void River(double dt, Controls c)
    {
        var p = Player;
        bool wasUnder = Under(p);
        Swim(p, c, Camera, dt);
        if (Under(p))
        {
            double was = p.Air;
            p.Air = Math.Max(0, p.Air - AirDrain * dt);
            // A warning each time it drops past 30, 20 and 10.
            if (p.Air < 30 && Math.Floor(was / 10) != Math.Floor(p.Air / 10)) Play(Sfx.AirLow);
        }
        else p.Air = Math.Min(100, p.Air + AirFill * dt);
        if (wasUnder != Under(p)) Play(Sfx.Splash);
        if (wasUnder && !Under(p) && p.Air < 70) Play(Sfx.Gulp);
        p.StabIn -= dt;
        p.Stab = Math.Max(0, p.Stab - dt);
        if ((c.Fire || c.Jump) && p.StabIn <= 0)
        {
            p.Stab = 0.25;
            p.StabIn = 0.45;
            Play(Sfx.Stab);
        }
        if (p.X > CheckX + 500) CheckX = p.X - 100;
        // The crocodiles come on as the screen reaches them.
        while (_crocIndex < _crocsToCome.Count && _crocsToCome[_crocIndex].X < Camera + Field.Width + 60)
        {
            if (!Calm) Crocs.Add(_crocsToCome[_crocIndex]);
            _crocIndex++;
        }
        MoveCrocs(dt);
        foreach (var k in Crocs.Where(k => !k.Dead))
        {
            // The knife: a crocodile with its jaws shut is done for.
            if (p.Stab > 0 && !k.JawsOpen)
            {
                double kx = p.X + p.Facing * KnifeReach, ky = p.Y;
                if (Math.Abs(kx - k.X) < Croc.Length / 2 - 12 && Math.Abs(ky - k.Y) < Croc.Thick)
                {
                    k.Dead = true;
                    Award(CrocPoints, k.X, k.Y - 30);
                    Play(Sfx.CrocDown);
                    continue;
                }
            }
            // Any touch of it is the end: the open mouth reaches further.
            if (p.Safe <= 0 && (Touches(k, p) || k.JawsOpen && Field.Distance(k.HeadX, k.Y, p.X, p.Y) < BiteReach)) { Kill("crocodile"); return; }
        }
        if (p.Air <= 0) { Kill("drowned"); return; }
        if (p.X >= SceneLength - 60) Done(SceneBonus, "ACROSS THE RIVER");
    }

    /// <summary>Whether a crocodile's body touches the explorer.</summary>
    public static bool Touches(Croc k, Explorer p) => !k.Dead && Math.Abs(p.X - k.X) < Croc.Length / 2 + 6 && Math.Abs(p.Y - k.Y) < Croc.Thick / 2 + 10;

    private void MoveCrocs(double dt)
    {
        foreach (var k in Crocs)
        {
            if (k.Dead)
            {
                // Belly up, drifting to the surface.
                k.DeadT += dt;
                k.Y = Math.Max(Field.Surface + 10, k.Y - 80 * dt);
                continue;
            }
            if (Calm) continue;
            k.T += dt;
            k.X -= k.Speed * dt;
        }
        Crocs.RemoveAll(k => k.X < Camera - 150 || k.DeadT > 3);
    }

    // ───────────────────────────── the hill ─────────────────────────────

    public const double RunSpeed = 190, JumpSpeed = 600;

    /// <summary>One step on foot on the hill (or the camp's flat ground): run, jump, duck.</summary>
    public static void Run(Explorer p, Controls c, Func<double, double> ground, double camera, double end, double dt)
    {
        p.Ducking = p.Grounded && c.Dy < 0;
        if (p.Grounded)
        {
            p.Vx = p.Ducking ? 0 : c.Dx * RunSpeed;
            if ((c.Jump || c.Dy > 0) && !p.Ducking)
            {
                p.Vy = -JumpSpeed;
                p.Grounded = false;
            }
        }
        else p.Vx += Math.Clamp(c.Dx * RunSpeed - p.Vx, -500 * dt, 500 * dt);
        if (c.Dx != 0) p.Facing = c.Dx;
        p.X = Math.Clamp(p.X + p.Vx * dt, camera + 20, end);
        if (!p.Grounded)
        {
            p.Vy += Field.Gravity * dt;
            p.Y += p.Vy * dt;
            if (p.Y >= ground(p.X)) { p.Y = ground(p.X); p.Vy = 0; p.Grounded = true; }
        }
        else
        {
            p.Y = ground(p.X);
            if (Math.Abs(p.Vx) > 1) p.Walk += dt;
        }
    }

    /// <summary>A boulder's step: rolling down the slope, bouncing off it (the big ones much higher).</summary>
    public static void Roll(Boulder b, double dt)
    {
        b.Vy += Field.Gravity * dt;
        b.X += b.Vx * dt;
        b.Y += b.Vy * dt;
        b.Spin += b.Vx / b.R * dt;
        double floor = Field.HillGround(b.X) - b.R;
        if (b.Y >= floor && b.Vy > 0)
        {
            b.Y = floor;
            b.Vy = -(b.Big ? 560 : 330);
        }
    }

    /// <summary>Whether a boulder touches the explorer (a circle against his standing, or ducking, shape).</summary>
    public static bool Hits(Boulder b, Explorer p)
    {
        double cx = Math.Clamp(b.X, p.X - Explorer.Width / 2, p.X + Explorer.Width / 2), cy = Math.Clamp(b.Y, p.Y - p.Tall, p.Y);
        return Field.Distance(cx, cy, b.X, b.Y) < b.R - 3;
    }

    private void Hill(double dt, Controls c)
    {
        var p = Player;
        bool was = p.Grounded;
        Run(p, c, Field.HillGround, Camera, SceneLength, dt);
        if (was && !p.Grounded) Play(Sfx.Jump);
        if (p.X > CheckX + 600) CheckX = p.X - 60;
        if (!Calm && p.X < SceneLength - 300 && (_boulderIn -= dt) <= 0)
        {
            _boulderIn = Math.Max(0.9, 1.7 - 0.1 * (Wave - 1)) * (0.8 + _rng.NextDouble() * 0.5);
            bool big = _rng.NextDouble() < Math.Min(0.5, 0.3 + 0.05 * (Wave - 1));
            double x = Camera + Field.Width + 60, r = big ? 42 : 22;
            Boulders.Add(new Boulder { X = x, Y = Field.HillGround(x) - r - 60, Vx = -(big ? 180 + 10 * Wave : 220 + 15 * Wave), R = r, Big = big });
            Play(Sfx.Rumble);
        }
        MoveBoulders(dt);
        foreach (var b in Boulders)
        {
            if (p.Safe <= 0 && Hits(b, p)) { Kill("boulder"); return; }
            if (!b.Passed && b.X < p.X - 40)
            {
                b.Passed = true;
                Award(b.Big ? BigBoulderPoints : BoulderPoints, p.X, p.Y - 80);
                Play(Sfx.Dodge);
            }
        }
        if (p.X >= SceneLength - 10) Done(SceneBonus, "UP THE HILL");
    }

    private void MoveBoulders(double dt)
    {
        if (Calm) return;
        foreach (var b in Boulders) Roll(b, dt);
        Boulders.RemoveAll(b => b.X < Camera - 120);
    }

    // ───────────────────────────── the camp ─────────────────────────────

    /// <summary>Whether a guard (at a time) touches the explorer.</summary>
    public static bool Hits(Guard g, double t, Explorer p)
    {
        var (gx, gy) = g.At(t);
        return Math.Abs(gx - p.X) < (Guard.Width + Explorer.Width) / 2 - 4 && p.Y > gy - Guard.Height + 6 && p.Y - p.Tall < gy - 4;
    }

    private void Camp(double dt, Controls c)
    {
        var p = Player;
        bool was = p.Grounded;
        Run(p, c, _ => Field.CampGround, Camera, SceneLength - 40, dt);
        if (was && !p.Grounded) Play(Sfx.Jump);
        double t = Calm ? 0 : T;
        foreach (var gd in Guards)
        {
            if (p.Safe <= 0 && Hits(gd, t, p)) { Kill("caught"); return; }
            if (!gd.Passed && p.X > gd.At(t).X + 40)
            {
                gd.Passed = true;
                Award(GuardPoints, p.X, p.Y - 90);
                CheckX = gd.Home + 90;
                Play(Sfx.Hop);
            }
        }
        if (Math.Abs(p.X - CaptiveX) < 34)
        {
            Play(Sfx.Rescue);
            Done(RescueBonus + 1000 * (Wave - 1), "RESCUED!");
        }
    }

    // ───────────────────────────── the rest ─────────────────────────────

    private void Done(int bonus, string words)
    {
        Bonus = bonus;
        Play(Sfx.SceneClear);
        Say(words, $"BONUS {bonus}", ClearSeconds);
        SetPhase(Phase.Clear);
        Player.Vx = 0;
    }

    private void Kill(string cause)
    {
        var p = Player;
        Cause = cause;
        p.Alive = false;
        p.Vine = -1;
        Lives--;
        Effects.Add(new Effect { Kind = EffectKind.Burst, X = p.X, Y = p.Y - 26, Life = 0.6 });
        Play(Sfx.Death);
        SetPhase(Phase.Dying);
        if (Lives <= 0) Say("GAME OVER", null, DyingSeconds);
    }

    private void Award(int points, double x, double y)
    {
        Score += points;
        Effects.Add(new Effect { Kind = EffectKind.Score, X = x, Y = y, Text = points.ToString(), Life = 0.8, Colour = 0xFFFFE14A });
        CheckExtra();
    }

    private void CheckExtra()
    {
        while (Score >= _nextExtra)
        {
            _nextExtra += ExtraLifeEvery;
            Lives++;
            Play(Sfx.ExtraLife);
        }
    }

    // ───────────────────────────── test hooks ─────────────────────────────

    internal void Run(int ticks, Controls c)
    {
        for (int i = 0; i < ticks; i++) Tick(c);
    }

    internal void Idle(int ticks) => Run(ticks, default);

    /// <summary>Straight into a scene of a round, past its name.</summary>
    internal void Go(Scene scene, int round = 1)
    {
        StartGame();
        Wave = round;
        BuildScene(scene);
        SetPhase(Phase.Play);
        Player.Safe = 0;
        Message = SubMessage = null;
    }

    internal void PutPlayer(double x, double y)
    {
        var p = Player;
        p.X = x;
        p.Y = y;
        p.Vx = p.Vy = 0;
        p.Vine = -1;
        p.Alive = true;
        p.Safe = 0;
        p.Grounded = Scene is Scene.Hill or Scene.Camp;
    }

    internal void Hang(int vine, double along)
    {
        Player.Vine = vine;
        Player.Along = along;
        Player.Safe = 0;
        HangOn(Player, Vines[vine], T);
    }

    internal void SetT(double t) => T = t;
    internal void SetLives(int n) => Lives = n;
    internal void SetScore(int n) => Score = n;
}
