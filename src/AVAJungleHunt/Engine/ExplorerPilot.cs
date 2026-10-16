namespace AVAJungleHunt.Engine;

/// <summary>
/// The computer player, for the demonstration (and the tests). It plays each scene by looking ahead with the
/// game's own movement:
/// <list type="bullet">
/// <item>On a vine, it tries letting go now: if that flight catches a vine further on (clear of its monkey), or
/// lands on the far bank, it lets go.</item>
/// <item>In the river, it swims along just under the surface, comes up for air in good time, stabs a crocodile
/// when its jaws are shut and it's in reach, and keeps away from an open mouth by changing depth or waiting.</item>
/// <item>On the hill and at the camp, it tries running on, jumping now, ducking, waiting and backing off, follows
/// each a second and a half ahead against the boulders or the guards, and takes the first that keeps it whole.</item>
/// </list>
/// </summary>
public sealed class ExplorerPilot
{
    /// <summary>What it is up to, for the probe.</summary>
    public string Doing { get; private set; } = "";

    public Controls Decide(GameEngine g)
    {
        var p = g.Player;
        if (g.Phase is not (Phase.Play or Phase.Start) || !p.Alive) { Doing = "waiting"; return default; }
        return g.Scene switch
        {
            Scene.Vines => Vines(g, p),
            Scene.River => River(g, p),
            Scene.Hill => OnFoot(g, p, hill: true),
            _ => OnFoot(g, p, hill: false),
        };
    }

    // ───────────────────────────── the vines ─────────────────────────────

    private Controls Vines(GameEngine g, Explorer p)
    {
        if (p.Vine < 0) { Doing = "flying"; return default; }
        if (g.Phase != Phase.Play) { Doing = "waiting"; return default; }
        var me = new Explorer { X = p.X, Y = p.Y, Vx = p.Vx, Vy = p.Vy };
        GameEngine.LetGo(me);
        double t = g.T;
        for (int i = 0; i < 120; i++)
        {
            t += Field.Step;
            double oldY = me.Y;
            if (GameEngine.Fly(me, g.Vines, p.Vine, t, Field.Step) is { } caught)
            {
                var v = g.Vines[caught.Vine];
                // Further on, and not into the monkey's arms (over the next second).
                bool monkey = v.Monkey && Enumerable.Range(0, 12).Any(k => Math.Abs(v.MonkeyAt(t + k * 0.1) - caught.Along) < 40);
                if (caught.Vine > p.Vine && !monkey) { Doing = "letting go"; return new Controls(Jump: true); }
                break;
            }
            if (oldY <= Field.LedgeTop && me.Y >= Field.LedgeTop && me.X >= g.Ledge.X0 && me.X <= g.Ledge.X1 && p.Vine == g.Vines.Count - 1) { Doing = "letting go"; return new Controls(Jump: true); }
            if (me.Y > Field.Swamp) break;
        }
        Doing = "swinging";
        return default;
    }

    // ───────────────────────────── the river ─────────────────────────────

    private const double Cruise = Field.Surface + 34;

    private Controls River(GameEngine g, Explorer p)
    {
        var live = g.Crocs.Where(k => !k.Dead && k.X > p.X - 60 && k.X < p.X + 420).OrderBy(k => k.X).ToList();
        // A crocodile in reach with its jaws shut: the knife.
        var near = live.FirstOrDefault(k => !k.JawsOpen && Math.Abs(p.X + GameEngine.KnifeReach - k.X) < Croc.Length / 2 - 16 && Math.Abs(k.Y - p.Y) < Croc.Thick - 4);
        if (near != null && p.StabIn <= 0) { Doing = "stabbing"; return new Controls(Dx: 0, Fire: true); }
        // Where it should be: up for air if it's running low, else cruising; away from any open mouth coming.
        double want = p.Air < 40 ? Field.Surface + 8 : Cruise;
        // A crocodile coming that it can't be sure of stabbing first (open now, or opening before it's in reach): out of its way.
        var danger = live.FirstOrDefault(k => k.HeadX - p.X < 180 && Math.Abs(k.Y - p.Y) < 60 && WillBeOpen(k, (k.HeadX - p.X) / (k.Speed + GameEngine.SwimSpeed)));
        int dx = 1;
        if (danger != null)
        {
            // Over it if there's room to the surface, else under it.
            double over = danger.Y - 70, under = danger.Y + 70;
            want = over > Field.Surface + 8 ? Math.Max(Field.Surface + 8, over) : Math.Min(Field.RiverBed - 22, under);
            if (Math.Abs(p.Y - want) > 30 && danger.HeadX - p.X < 110) dx = -1;
            Doing = "dodging";
        }
        else Doing = p.Air < 40 ? "coming up for air" : near != null ? "waiting to stab" : "swimming";
        // Waiting a moment for a shut mouth in front to come into reach.
        if (danger == null && live.FirstOrDefault(k => k.X - p.X < 70 && Math.Abs(k.Y - p.Y) < Croc.Thick) is { } close && p.StabIn > 0) dx = 0;
        int dy = Math.Abs(p.Y - want) < 6 ? 0 : p.Y > want ? 1 : -1;
        return new Controls(dx, dy);
    }

    private static bool WillBeOpen(Croc k, double within)
    {
        for (double t = 0; t <= Math.Max(0.1, within) + 0.3; t += 0.05)
            if ((k.T + t) % k.Cycle < k.Open) return true;
        return false;
    }

    // ───────────────────────────── on foot: the hill and the camp ─────────────────────────────

    private static readonly (string Name, Controls C)[] Options =
    [
        ("running", new Controls(Dx: 1)),
        ("jumping", new Controls(Dx: 1, Jump: true)),
        ("ducking", new Controls(Dy: -1)),
        ("jumping on the spot", new Controls(Jump: true)),
        ("waiting", default),
        ("backing off", new Controls(Dx: -1)),
    ];

    private Controls OnFoot(GameEngine g, Explorer p, bool hill)
    {
        if (!p.Grounded) { Doing = "in the air"; return new Controls(Dx: 1); }
        foreach (var (name, c) in Options)
        {
            if (Survives(g, p, c, hill)) { Doing = name; return c; }
        }
        Doing = "trapped";
        return new Controls(Dy: -1);
    }

    /// <summary>Whether doing this now (and then, for the rest of the look ahead, the best of running or ducking) keeps it clear for a second and a half.</summary>
    private static bool Survives(GameEngine g, Explorer p, Controls first, bool hill)
    {
        var me = new Explorer { X = p.X, Y = p.Y, Vx = p.Vx, Vy = p.Vy, Grounded = p.Grounded };
        var rocks = g.Boulders.Select(b => new Boulder { X = b.X, Y = b.Y, Vx = b.Vx, Vy = b.Vy, R = b.R, Big = b.Big }).ToList();
        Func<double, double> ground = hill ? Field.HillGround : _ => Field.CampGround;
        double t = g.T;
        for (int i = 0; i < 90; i++)
        {
            // The first choice held for a moment (all the way through a jump), then just standing its ground.
            var c = i < 12 || (!me.Grounded && first.Jump) ? first : first.Dy < 0 ? first : default;
            if (i >= 12 && me.Grounded) c = first.Dy < 0 ? first : new Controls();
            GameEngine.Run(me, c, ground, g.Camera, g.SceneLength, Field.Step);
            t += Field.Step;
            if (hill)
            {
                foreach (var b in rocks)
                {
                    GameEngine.Roll(b, Field.Step);
                    if (GameEngine.Hits(b, me)) return false;
                }
            }
            else if (g.Guards.Any(gd => GameEngine.Hits(gd, t, me))) return false;
        }
        return true;
    }
}
