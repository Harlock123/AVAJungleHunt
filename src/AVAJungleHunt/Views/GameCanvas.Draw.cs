using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AVAJungleHunt.Engine;
using Effect = AVAJungleHunt.Engine.Effect;

namespace AVAJungleHunt.Views;

public sealed partial class GameCanvas
{
    private static readonly Dictionary<uint, IBrush> BrushCache = [];
    private static readonly Dictionary<(uint, double), Pen> PenCache = [];
    private static readonly Typeface Retro = new(new FontFamily("avares://AVAJungleHunt/Assets/Fonts#VT323"));

    public static IBrush Brush(uint argb)
    {
        if (!BrushCache.TryGetValue(argb, out var b))
            BrushCache[argb] = b = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
        return b;
    }

    private static Pen Pen(uint argb, double width = 1)
    {
        if (!PenCache.TryGetValue((argb, width), out var p))
            PenCache[(argb, width)] = p = new Pen(Brush(argb), width, lineCap: PenLineCap.Round);
        return p;
    }

    private const uint White = 0xFFF2F2FF, Yellow = 0xFFFFE14A, Red = 0xFFFF4A3A, Dim = 0xFF8A9AC8, Fire = 0xFFFF8A2A, Cyan = 0xFF6AE0FF;

    private static void Text(DrawingContext dc, string text, Point at, double size, uint colour, bool center = false, bool right = false, bool shadow = true)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, Brush(colour));
        double x = center ? at.X - ft.Width / 2 : right ? at.X - ft.Width : at.X;
        if (shadow) dc.DrawText(new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, Brush(0xC0000000)), new Point(x + 2, at.Y - ft.Height / 2 + 2));
        dc.DrawText(ft, new Point(x, at.Y - ft.Height / 2));
    }

    private static void Poly(DrawingContext dc, uint fill, uint? edge, double width, params Point[] pts)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(pts[0], true);
            foreach (var p in pts.Skip(1)) c.LineTo(p);
            c.EndFigure(true);
        }
        dc.DrawGeometry(fill == 0 ? null : Brush(fill), edge is { } e ? Pen(e, width) : null, g);
    }

    private static uint Hash(int x, int y, int salt)
    {
        uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(salt * 83492791);
        h ^= h >> 13;
        h *= 0x5bd1e995;
        h ^= h >> 15;
        return h;
    }

    private static uint Alpha(uint colour, double a) => (uint)(Math.Clamp(a, 0, 1) * 255) << 24 | (colour & 0x00FFFFFF);
    private bool InGame => Engine.State is GameState.Playing or GameState.Attract or GameState.Paused or GameState.GameOver;

    private const uint Skin = 0xFFE8B888, Khaki = 0xFFC8B078, Helmet = 0xFFEEE6CC, Leaf = 0xFF2A6A2A, LeafDark = 0xFF184818;

    private double Sx(double x) => x - Engine.Camera;

    private void DrawScreen(DrawingContext dc)
    {
        var g = Engine;
        if (InGame)
        {
            switch (g.Scene)
            {
                case Scene.Vines: DrawVines(dc, g); break;
                case Scene.River: DrawRiver(dc, g); break;
                case Scene.Hill: DrawHill(dc, g); break;
                default: DrawCamp(dc, g); break;
            }
        }
        else DrawJungle(dc, 0, 0xFF0E2A12);
        foreach (var e in g.Effects) DrawEffect(dc, e);
        DrawHud(dc);
        switch (g.State)
        {
            case GameState.Title: DrawTitle(dc); break;
            case GameState.HighScores: DrawHighScores(dc); break;
            case GameState.EnterInitials: DrawInitials(dc); break;
            default: DrawMessages(dc); break;
        }
        if (g.State == GameState.Paused)
        {
            dc.FillRectangle(Brush(0x90000000), new Rect(0, 0, Field.Width, Field.Height));
            Text(dc, "PAUSED", new Point(Field.Width / 2, 340), 80, Yellow, center: true);
            Text(dc, "ANY KEY TO CARRY ON - ESC TO QUIT", new Point(Field.Width / 2, 400), 26, White, center: true);
        }
        if (g.State == GameState.GameOver)
            Text(dc, "GAME OVER", new Point(Field.Width / 2, 300), 100, Red, center: true);
        if (g.State == GameState.Attract)
            Text(dc, "DEMONSTRATION - PRESS ENTER TO PLAY", new Point(Field.Width / 2, Field.Height - 14), 24, Yellow, center: true);
    }

    /// <summary>The jungle behind: a dim wall of trunks and leaves, sliding past slower than the scene.</summary>
    private void DrawJungle(DrawingContext dc, double camera, uint sky)
    {
        dc.FillRectangle(Brush(sky), new Rect(0, 0, Field.Width, Field.Height));
        double off = camera * 0.3;
        for (int i = -1; i < 14; i++)
        {
            int n = i + (int)Math.Floor(off / 90);
            uint h = Hash(n, 1, 7);
            double x = n * 90 - off + h % 40;
            dc.FillRectangle(Brush(0xFF1A3014), new Rect(x, 60, 14 + h % 10, Field.Height));
            dc.DrawEllipse(Brush(0xFF1E4018), null, new Point(x + 8, 70 + (h >> 8) % 60), 60, 40);
        }
    }

    // ───────────────────────────── the vines ─────────────────────────────

    private void DrawVines(DrawingContext dc, GameEngine g)
    {
        DrawJungle(dc, g.Camera, 0xFF0E2A12);
        // The swamp below.
        dc.FillRectangle(Brush(0xFF2A3A1A), new Rect(0, Field.Swamp - 20, Field.Width, Field.Height));
        for (int i = 0; i < 30; i++)
        {
            uint h = Hash(i, 2, 3);
            double x = (h % 1100 - g.Camera * 0.9) % 1100;
            if (x < 0) x += 1100;
            dc.DrawEllipse(null, Pen(0x60A0C080, 1.5), new Point(x, Field.Swamp + (h >> 10) % 40), 6 + Math.Sin(_clock * 2 + i) * 2, 2);
        }
        // The far bank.
        double lx = Sx(g.Ledge.X0);
        if (lx < Field.Width)
        {
            dc.FillRectangle(Brush(0xFF5A3A1A), new Rect(lx, Field.LedgeTop, g.Ledge.X1 - g.Ledge.X0, Field.Height - Field.LedgeTop));
            dc.FillRectangle(Brush(0xFF3A8A2A), new Rect(lx, Field.LedgeTop - 6, g.Ledge.X1 - g.Ledge.X0, 10));
        }
        // The canopy.
        for (int i = -1; i < 20; i++)
        {
            int n = i + (int)Math.Floor(g.Camera / 60);
            double x = n * 60 - g.Camera;
            dc.DrawEllipse(Brush(n % 2 == 0 ? Leaf : LeafDark), null, new Point(x, Field.Canopy - 30), 48, 42);
        }
        foreach (var v in g.Vines)
        {
            if (Sx(v.X) < -250 || Sx(v.X) > Field.Width + 250) continue;
            var (tx, ty) = v.At(v.Length, g.T);
            dc.DrawLine(Pen(0xFF5A8A2A, 4), new Point(Sx(v.X), Field.Canopy), new Point(Sx(tx), ty));
            for (double d = 40; d < v.Length; d += 46)
            {
                var (lx2, ly) = v.At(d, g.T);
                dc.DrawEllipse(Brush(0xFF3A9A3A), null, new Point(Sx(lx2) + 6, ly), 7, 3.5);
            }
            if (v.Monkey)
            {
                var (mx, my) = v.At(v.MonkeyAt(g.T), g.T);
                DrawMonkey(dc, Sx(mx), my);
            }
        }
        DrawExplorer(dc, g);
    }

    private void DrawMonkey(DrawingContext dc, double x, double y)
    {
        dc.DrawEllipse(Brush(0xFF7A4A22), Pen(0xFF3A2010, 1), new Point(x + 6, y + 6), 9, 12);
        dc.DrawEllipse(Brush(0xFF7A4A22), Pen(0xFF3A2010, 1), new Point(x + 8, y - 10), 8, 8);
        dc.DrawEllipse(Brush(0xFFE0B080), null, new Point(x + 10, y - 9), 5, 4.5);
        dc.DrawEllipse(Brush(0xFF101010), null, new Point(x + 12, y - 11), 1.2, 1.2);
        dc.DrawLine(Pen(0xFF7A4A22, 3), new Point(x + 2, y), new Point(x - 1, y - 8 + Math.Sin(_clock * 8) * 3));
        dc.DrawLine(Pen(0xFF7A4A22, 3), new Point(x + 2, y + 16), new Point(x - 6, y + 22 + Math.Sin(_clock * 5) * 4));
    }

    // ───────────────────────────── the river ─────────────────────────────

    private void DrawRiver(DrawingContext dc, GameEngine g)
    {
        dc.FillRectangle(Brush(0xFF6AA8D8), new Rect(0, 0, Field.Width, Field.Surface));
        // The far bank's trees above the water.
        double off = g.Camera * 0.4;
        for (int i = -1; i < 16; i++)
        {
            int n = i + (int)Math.Floor(off / 80);
            uint h = Hash(n, 5, 2);
            double x = n * 80 - off;
            dc.FillRectangle(Brush(0xFF5A3A1A), new Rect(x + 30, Field.Surface - 120, 10, 120));
            dc.DrawEllipse(Brush(n % 2 == 0 ? Leaf : 0xFF3A7A2A), null, new Point(x + 35, Field.Surface - 130 - h % 30), 50, 40);
        }
        dc.FillRectangle(Brush(0xFF2A5A8A), new Rect(0, Field.Surface, Field.Width, Field.Height - Field.Surface));
        dc.FillRectangle(Brush(0xFF1A4070), new Rect(0, 480, Field.Width, Field.Height - 480));
        dc.FillRectangle(Brush(0xFFB09A6A), new Rect(0, Field.RiverBed, Field.Width, Field.Height - Field.RiverBed));
        for (int i = 0; i < 26; i++)
        {
            double x = (i * 44 - g.Camera) % (26 * 44);
            if (x < 0) x += 26 * 44;
            dc.DrawLine(Pen(0x90C8E8FF, 2), new Point(x, Field.Surface + Math.Sin(_clock * 3 + i) * 3), new Point(x + 22, Field.Surface + Math.Sin(_clock * 3 + i + 1) * 3));
            dc.DrawLine(Pen(0xFF2A6A2A, 3), new Point(x + 10, Field.RiverBed), new Point(x + 10 + Math.Sin(_clock + i) * 6, Field.RiverBed - 30 - i % 3 * 10));
        }
        foreach (var k in g.Crocs) DrawCroc(dc, Sx(k.X), k.Y, k.JawsOpen, k.Dead, k.T);
        DrawExplorer(dc, g);
        // Air.
        var p = g.Player;
        dc.DrawRectangle(Brush(0xA0000000), Pen(0xFFFFFFFF, 1.5), new Rect(Field.Width / 2 - 100, 72, 200, 14), 3, 3);
        dc.FillRectangle(Brush(p.Air < 30 ? Red : 0xFF6AE0FF), new Rect(Field.Width / 2 - 98, 74, 196 * p.Air / 100, 10));
        Text(dc, "AIR", new Point(Field.Width / 2 - 120, 79), 18, White, right: true);
    }

    /// <summary>A crocodile, swimming left: long body, ridged back, tail, and its jaws (gaping when open); belly up when it's done for.</summary>
    private void DrawCroc(DrawingContext dc, double x, double y, bool open, bool dead, double t)
    {
        uint body = dead ? 0xFFC8D0A0 : 0xFF3A7A3A, dark = dead ? 0xFF8A9070 : 0xFF1A4A1A;
        double sway = dead ? 0 : Math.Sin(t * 6) * 4;
        Poly(dc, body, dark, 1.5, new Point(x + 55, y + sway), new Point(x + 25, y - 8), new Point(x + 25, y + 8));
        dc.DrawRectangle(Brush(body), Pen(dark, 1.5), new Rect(x - 35, y - 13, 62, 26), 12, 12);
        if (!dead) for (int i = 0; i < 5; i++) Poly(dc, dark, null, 0, new Point(x - 28 + i * 12, y - 12), new Point(x - 22 + i * 12, y - 18), new Point(x - 16 + i * 12, y - 12));
        // The jaws.
        double gape = open ? 0.5 : 0.05;
        Poly(dc, body, dark, 1.5, new Point(x - 32, y - 4), new Point(x - 70, y - 4 - Math.Sin(gape) * 34), new Point(x - 72, y - Math.Sin(gape) * 34 + 2), new Point(x - 30, y + 2));
        Poly(dc, body, dark, 1.5, new Point(x - 32, y + 2), new Point(x - 70, y + 6), new Point(x - 68, y + 12), new Point(x - 30, y + 10));
        if (open)
        {
            dc.DrawLine(Pen(0xFFFFFFFF, 2), new Point(x - 66, y - Math.Sin(gape) * 30), new Point(x - 40, y - 6));
            dc.DrawLine(Pen(0xFFFFFFFF, 2), new Point(x - 66, y + 5), new Point(x - 40, y + 3));
            dc.DrawEllipse(Brush(0xFFB03A3A), null, new Point(x - 44, y - 2), 8, 4);
        }
        dc.DrawEllipse(Brush(0xFFFFE040), null, new Point(x - 28, y - 10), 3.5, 3);
        dc.DrawEllipse(Brush(0xFF101010), null, new Point(x - 29, y - 10), 1.2, 2);
        foreach (int leg in new[] { -1, 1 })
            dc.DrawLine(Pen(dark, 4), new Point(x - 10 + leg * 18, y + 10), new Point(x - 14 + leg * 18 + sway, y + 20));
    }

    // ───────────────────────────── the hill ─────────────────────────────

    private void DrawHill(DrawingContext dc, GameEngine g)
    {
        dc.FillRectangle(Brush(0xFFE89A5A), new Rect(0, 0, Field.Width, 300));
        dc.FillRectangle(Brush(0xFFD8784A), new Rect(0, 300, Field.Width, Field.Height - 300));
        dc.DrawEllipse(Brush(0xFFFFE08A), null, new Point(820, 160), 60, 60);
        double off = g.Camera * 0.2;
        Poly(dc, 0xFF9A5A4A, null, 0, new Point(-off % 400 - 400, 420), new Point(-off % 400 - 200, 220), new Point(-off % 400, 420), new Point(-off % 400 + 200, 260), new Point(-off % 400 + 400, 420),
            new Point(-off % 400 + 600, 200), new Point(-off % 400 + 800, 420), new Point(-off % 400 + 1000, 240), new Point(-off % 400 + 1200, 420), new Point(-off % 400 + 1400, 300), new Point(-off % 400 + 1600, 420), new Point(-off % 400 + 1600, 800), new Point(-off % 400 - 400, 800));
        // The slope.
        var pts = new List<Point>();
        for (double x = 0; x <= Field.Width; x += 16) pts.Add(new Point(x, Field.HillGround(x + g.Camera)));
        pts.Add(new Point(Field.Width, Field.Height));
        pts.Add(new Point(0, Field.Height));
        Poly(dc, 0xFF6A4A2A, 0xFF3A8A2A, 4, pts.ToArray());
        for (int i = 0; i < 40; i++)
        {
            uint h = Hash(i, 9, 9);
            double wx = (h % 2600), sx = Sx(wx);
            if (sx < -20 || sx > Field.Width + 20) continue;
            dc.DrawEllipse(Brush(0xFF5A3A1A), null, new Point(sx, Field.HillGround(wx) + 30 + (h >> 12) % 60), 6, 3);
        }
        // The summit marker.
        double fx = Sx(g.SceneLength);
        if (fx < Field.Width + 40)
        {
            dc.DrawLine(Pen(0xFF5A3A1A, 4), new Point(fx, Field.HillGround(g.SceneLength)), new Point(fx, Field.HillGround(g.SceneLength) - 90));
            Poly(dc, 0xFFE03A2A, null, 0, new Point(fx, Field.HillGround(g.SceneLength) - 90), new Point(fx + 40, Field.HillGround(g.SceneLength) - 78), new Point(fx, Field.HillGround(g.SceneLength) - 66));
        }
        foreach (var b in g.Boulders)
        {
            var c = new Point(Sx(b.X), b.Y);
            dc.DrawEllipse(Brush(0x40000000), null, new Point(c.X, Field.HillGround(b.X) - 2), b.R, b.R * 0.25);
            dc.DrawEllipse(Brush(0xFF8A8A8A), Pen(0xFF4A4A4A, 2), c, b.R, b.R);
            for (int i = 0; i < 3; i++)
            {
                double a = b.Spin + i * 2.1;
                dc.DrawLine(Pen(0xFF5A5A5A, 2), new Point(c.X + Math.Cos(a) * b.R * 0.2, c.Y + Math.Sin(a) * b.R * 0.2), new Point(c.X + Math.Cos(a) * b.R * 0.8, c.Y + Math.Sin(a) * b.R * 0.8));
            }
            dc.DrawEllipse(Brush(0x50FFFFFF), null, new Point(c.X - b.R * 0.35, c.Y - b.R * 0.35), b.R * 0.25, b.R * 0.2);
        }
        DrawExplorer(dc, g);
    }

    // ───────────────────────────── the camp ─────────────────────────────

    private void DrawCamp(DrawingContext dc, GameEngine g)
    {
        dc.FillRectangle(Brush(0xFF3A2A5A), new Rect(0, 0, Field.Width, Field.Height));
        dc.FillRectangle(Brush(0xFF6A3A5A), new Rect(0, 360, Field.Width, 200));
        DrawJungle(dc, g.Camera, 0x00000000);
        dc.FillRectangle(Brush(0xFF5A4A2A), new Rect(0, Field.CampGround, Field.Width, Field.Height - Field.CampGround));
        dc.FillRectangle(Brush(0xFF3A6A2A), new Rect(0, Field.CampGround - 4, Field.Width, 8));
        // Tents, crates and a fire.
        foreach (double wx in new[] { 260.0, 880, 1460 })
        {
            double x = Sx(wx);
            if (x < -150 || x > Field.Width + 150) continue;
            Poly(dc, 0xFF8A7A4A, 0xFF4A3A1A, 2, new Point(x - 70, Field.CampGround), new Point(x, Field.CampGround - 110), new Point(x + 70, Field.CampGround));
            Poly(dc, 0xFF2A2010, null, 0, new Point(x - 14, Field.CampGround), new Point(x, Field.CampGround - 60), new Point(x + 14, Field.CampGround));
        }
        foreach (double wx in new[] { 560.0, 1150 })
        {
            double x = Sx(wx);
            dc.DrawRectangle(Brush(0xFF9A6A30), Pen(0xFF4A3010, 2), new Rect(x - 22, Field.CampGround - 40, 44, 40));
            for (int i = 0; i < 5; i++)
            {
                double fl = Math.Sin(_clock * 12 + i * 1.7) * 6;
                Poly(dc, i % 2 == 0 ? 0xFFFFA02A : 0xFFFFE06A, null, 0, new Point(x + 40 + i * 6, Field.CampGround), new Point(x + 46 + i * 6 + fl * 0.3, Field.CampGround - 26 - fl), new Point(x + 52 + i * 6, Field.CampGround));
            }
        }
        // The captive, tied to a post, and the guards.
        double cx = Sx(g.CaptiveX);
        dc.FillRectangle(Brush(0xFF5A3A1A), new Rect(cx + 8, Field.CampGround - 90, 10, 90));
        if (g.Phase == Phase.Clear)
        {
            // Free: the ropes off, and a wave.
            DrawPerson(dc, cx - 20, Field.CampGround, -1, 0, Pose.Jump, 0xFFE0E0E0, 0xFF4A6AA0, 0xFF6A4A2A, 1);
            Text(dc, "THANK YOU!", new Point(cx - 20, Field.CampGround - 100), 22, Yellow, center: true);
        }
        else
        {
            DrawPerson(dc, cx, Field.CampGround, -1, 0, Pose.Tied, 0xFFE0E0E0, 0xFF4A6AA0, 0xFF6A4A2A, 1);
            if ((int)(_clock * 2) % 2 == 0) Text(dc, "HELP!", new Point(cx, Field.CampGround - 100), 22, White, center: true);
        }
        double t = g.T;
        foreach (var gd in g.Guards)
        {
            var (gx, gy) = gd.At(t);
            DrawPerson(dc, Sx(gx), gy, -1, t, gy < Field.CampGround - 2 ? Pose.Jump : Pose.Stand, 0xFF6A6A3A, 0xFF4A4A2A, 0xFF3A3A2A, 1.1, poacher: true);
        }
        DrawExplorer(dc, g);
    }

    // ───────────────────────────── people ─────────────────────────────

    private enum Pose { Stand, Run, Jump, Duck, Hang, Swim, Tied }

    private void DrawExplorer(DrawingContext dc, GameEngine g)
    {
        var p = g.Player;
        if (!p.Alive)
        {
            if ((int)(_clock * 8) % 2 == 0) DrawPerson(dc, Sx(p.X), p.Y, p.Facing, 0, g.Scene == Scene.River ? Pose.Swim : Pose.Duck, Khaki, 0xFF6A4A2A, Helmet, 1);
            return;
        }
        if (p.Safe > 0 && g.Phase == Phase.Play && (int)(_clock * 10) % 2 == 0) return;
        var pose = g.Scene switch
        {
            Scene.Vines => p.Vine >= 0 ? Pose.Hang : p.Grounded ? Pose.Stand : Pose.Jump,
            Scene.River => Pose.Swim,
            _ => p.Ducking ? Pose.Duck : !p.Grounded ? Pose.Jump : Math.Abs(p.Vx) > 1 ? Pose.Run : Pose.Stand,
        };
        DrawPerson(dc, Sx(p.X), p.Y, p.Facing, p.Walk, pose, Khaki, 0xFF6A4A2A, Helmet, 1, stab: p.Stab > 0);
    }

    /// <summary>
    /// A person, side on, standing on (x, y): boots, legs, a shirt, arms, and a head under a hat. The explorer wears
    /// a pith helmet; a poacher a wide hat and carries a long net pole. Swimming, he lies along the water with his
    /// knife out in front.
    /// </summary>
    private static void DrawPerson(DrawingContext dc, double x, double y, int facing, double walk, Pose pose, uint shirt, uint shorts, uint hat, double k, bool stab = false, bool poacher = false)
    {
        double f = facing;
        if (pose == Pose.Swim)
        {
            // Lying in the water, head to the front.
            double kick = Math.Sin(walk * 10) * 5;
            dc.DrawLine(Pen(Skin, 5 * k), new Point(x - f * 18 * k, y + 2), new Point(x - f * 34 * k, y + kick));
            dc.DrawLine(Pen(Skin, 5 * k), new Point(x - f * 18 * k, y + 6), new Point(x - f * 34 * k, y + 8 - kick));
            dc.DrawRectangle(Brush(shorts), null, new Rect(x - (f > 0 ? 22 : 4) * k, y - 4 * k, 18 * k, 12 * k), 3, 3);
            dc.DrawRectangle(Brush(shirt), Pen(0xFF6A5A30, 1), new Rect(x - (f > 0 ? 6 : -6 + 20) * k, y - 6 * k, 20 * k, 13 * k), 4, 4);
            dc.DrawEllipse(Brush(Skin), Pen(0xFF7A5A3A, 1), new Point(x + f * 20 * k, y - 2 * k), 7 * k, 7 * k);
            dc.DrawEllipse(Brush(hat), Pen(0xFF8A7A5A, 1), new Point(x + f * 19 * k, y - 7 * k), 8 * k, 4.5 * k);
            double reach = stab ? 30 : 14;
            dc.DrawLine(Pen(Skin, 4 * k), new Point(x + f * 12 * k, y + 2 * k), new Point(x + f * (12 + reach) * k, y + 4 * k));
            dc.DrawLine(Pen(0xFFE0E0F0, 3), new Point(x + f * (12 + reach) * k, y + 4 * k), new Point(x + f * (24 + reach) * k, y + 4 * k));
            return;
        }
        double tall = pose == Pose.Duck ? 0.55 : 1;
        double stride = pose == Pose.Run ? Math.Sin(walk * 14) * 7 * k : 0;
        dc.DrawEllipse(Brush(0x40000000), null, new Point(x, y), 12 * k, 3 * k);
        // Legs and boots.
        foreach (int leg in new[] { -1, 1 })
        {
            double lx = x + leg * stride + (pose == Pose.Jump ? leg * 5 * k : 0), top = y - 24 * k * tall;
            dc.DrawLine(Pen(Skin, 4.5 * k), new Point(x + leg * 3 * k, top), new Point(lx, y - 4 * k));
            dc.DrawRectangle(Brush(0xFF4A3018), null, new Rect(lx - 4 * k + f * 2 * k, y - 6 * k, 10 * k, 6 * k), 2, 2);
        }
        double body = y - 24 * k * tall;
        dc.DrawRectangle(Brush(shorts), null, new Rect(x - 8 * k, body - 6 * k, 16 * k, 10 * k), 3, 3);
        dc.DrawRectangle(Brush(shirt), Pen(0xFF5A4A2A, 1), new Rect(x - 9 * k, body - 24 * k * tall, 18 * k, 20 * k * tall), 4, 4);
        double shoulder = body - 22 * k * tall;
        // Arms.
        if (pose == Pose.Hang)
        {
            dc.DrawLine(Pen(shirt, 4.5 * k), new Point(x - 5 * k, shoulder), new Point(x - 1 * k, y - 52 * k));
            dc.DrawLine(Pen(shirt, 4.5 * k), new Point(x + 5 * k, shoulder), new Point(x + 1 * k, y - 52 * k));
        }
        else if (pose == Pose.Tied)
        {
            dc.DrawLine(Pen(shirt, 4.5 * k), new Point(x - 6 * k, shoulder), new Point(x + 10 * k, shoulder + 10 * k));
            dc.DrawLine(Pen(0xFFC8A060, 2), new Point(x - 10 * k, shoulder + 6 * k), new Point(x + 14 * k, shoulder + 6 * k));
            dc.DrawLine(Pen(0xFFC8A060, 2), new Point(x - 10 * k, body), new Point(x + 14 * k, body));
            dc.DrawRectangle(Brush(0xFF2A2A2A), null, new Rect(x - 5 * k, shoulder + 2 * k, 9 * k, 7 * k), 1, 1);
        }
        else
        {
            double swing = pose == Pose.Run ? Math.Sin(walk * 14) * 6 * k : 0;
            dc.DrawLine(Pen(shirt, 4.5 * k), new Point(x - f * 5 * k, shoulder), new Point(x - f * 8 * k - swing, shoulder + 16 * k));
            dc.DrawLine(Pen(shirt, 4.5 * k), new Point(x + f * 5 * k, shoulder), new Point(x + f * 10 * k + swing, shoulder + (pose == Pose.Jump ? -6 : 14) * k));
            if (poacher)
            {
                // A long net pole held up.
                dc.DrawLine(Pen(0xFF6A4A2A, 3), new Point(x + f * 10 * k, shoulder + 10 * k), new Point(x + f * 20 * k, shoulder - 40 * k));
                dc.DrawEllipse(null, Pen(0xFFB0B0A0, 1.5), new Point(x + f * 22 * k, shoulder - 48 * k), 9 * k, 9 * k);
            }
        }
        // Head and hat.
        var head = new Point(x + f * 1 * k, shoulder - 9 * k);
        dc.DrawEllipse(Brush(Skin), Pen(0xFF7A5A3A, 1), head, 7 * k, 7 * k);
        if (pose != Pose.Hang) dc.DrawEllipse(Brush(0xFF101010), null, new Point(head.X + f * 3.5 * k, head.Y), 1.2 * k, 1.4 * k);
        if (poacher)
        {
            dc.DrawEllipse(Brush(hat), null, new Point(head.X, head.Y - 5 * k), 13 * k, 3.5 * k);
            dc.DrawEllipse(Brush(hat), null, new Point(head.X, head.Y - 8 * k), 7 * k, 5 * k);
            dc.DrawLine(Pen(0xFF2A2A1A, 2), new Point(head.X - 6 * k, head.Y + 3 * k), new Point(head.X + 6 * k, head.Y + 3 * k));
        }
        else
        {
            dc.DrawEllipse(Brush(hat), Pen(0xFF9A8A6A, 1), new Point(head.X, head.Y - 6 * k), 9 * k, 7 * k);
            dc.DrawEllipse(Brush(hat), Pen(0xFF9A8A6A, 1), new Point(head.X, head.Y - 3 * k), 12 * k, 3 * k);
            dc.DrawLine(Pen(0xFF8A6A3A, 2), new Point(head.X - 9 * k, head.Y - 4 * k), new Point(head.X + 9 * k, head.Y - 4 * k));
        }
    }

    private void DrawEffect(DrawingContext dc, Effect e)
    {
        double t = e.Age / e.Life;
        var c = new Point(Sx(e.X), e.Y);
        switch (e.Kind)
        {
            case EffectKind.Burst:
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4, r = 6 + 34 * t;
                    dc.DrawLine(Pen(Alpha(0xFFFFFFFF, 1 - t), 3), new Point(c.X + Math.Cos(a) * r * 0.5, c.Y + Math.Sin(a) * r * 0.5), new Point(c.X + Math.Cos(a) * r, c.Y + Math.Sin(a) * r));
                }
                break;
            case EffectKind.Score:
                Text(dc, e.Text, new Point(c.X, c.Y - t * 26), 24, e.Colour, center: true);
                break;
            case EffectKind.Word:
                Text(dc, e.Text, new Point(c.X, c.Y - t * 30), 30, e.Colour, center: true);
                break;
        }
    }

    // ───────────────────────────── the score bar, and the screens ─────────────────────────────

    private void DrawHud(DrawingContext dc)
    {
        var g = Engine;
        dc.FillRectangle(Brush(0x80000000), new Rect(0, 0, Field.Width, 60));
        Text(dc, "SCORE", new Point(16, 16), 22, Dim);
        Text(dc, g.Score.ToString("D6"), new Point(16, 42), 32, Yellow);
        Text(dc, "HI", new Point(Field.Width - 16, 16), 22, Dim, right: true);
        Text(dc, Math.Max(g.HighScores.TopScore, g.Score).ToString("D6"), new Point(Field.Width - 16, 42), 28, Dim, right: true);
        if (!InGame) return;
        Text(dc, $"ROUND {g.Wave}  {GameEngine.NameOf(g.Scene)}", new Point(512, 18), 24, 0xFF8AE08A, center: true);
        // How far along the scene.
        double frac = Math.Clamp(g.Player.X / Math.Max(1, g.SceneLength), 0, 1);
        dc.DrawRectangle(null, Pen(0xFF8AE08A, 1.5), new Rect(412, 40, 200, 8), 2, 2);
        dc.FillRectangle(Brush(0xFF8AE08A), new Rect(414, 42, 196 * frac, 4));
        int spare = Math.Max(0, g.Lives - (g.Player.Alive ? 1 : 0));
        for (int i = 0; i < Math.Min(spare, 6); i++) DrawPerson(dc, 200 + i * 24, 56, 1, 0, Pose.Stand, Khaki, 0xFF6A4A2A, Helmet, 0.55);
    }

    private void DrawMessages(DrawingContext dc)
    {
        var g = Engine;
        if (g.Message is not { } msg) return;
        double cx = Field.Width / 2, cy = 190;
        var (w, h) = (Math.Max(msg.Length * 26, (g.SubMessage?.Length ?? 0) * 15) + 60, g.SubMessage != null ? 106.0 : 70.0);
        dc.FillRectangle(Brush(0xD0102010), new Rect(cx - w / 2, cy - 46, w, h), 6);
        dc.DrawRectangle(null, Pen(0xFF8AE08A, 2), new Rect(cx - w / 2, cy - 46, w, h), 6);
        Text(dc, msg, new Point(cx, cy - 12), msg.Length > 14 ? 42 : 54, g.Phase == Phase.Dying ? Red : Yellow, center: true);
        if (g.SubMessage is { } sub) Text(dc, sub, new Point(cx, cy + 34), 26, White, center: true);
    }

    private void DrawTitle(DrawingContext dc)
    {
        double cx = Field.Width / 2;
        string title = "AVA JUNGLE HUNT";
        uint[] hues = [0xFF8AE08A, 0xFFFFD050, 0xFFE8B888, 0xFF6AD0FF];
        for (int i = 0; i < title.Length; i++)
            Text(dc, title[i].ToString(), new Point(cx - title.Length * 25 + 25 + i * 50, 104 + Math.Sin(_clock * 3 + i * 0.5) * 4), 100, hues[(i + (int)(_clock * 2)) % hues.Length], center: true);
        Text(dc, "AFTER THE 1982 ARCADE CLASSIC", new Point(cx, 166), 28, Dim, center: true);
        // The explorer swinging on a vine.
        double a = Math.Sin(_clock * 1.8) * 0.6;
        var anchor = new Point(cx, 190);
        var tip = new Point(cx + Math.Sin(a) * 120, 190 + Math.Cos(a) * 120);
        dc.DrawLine(Pen(0xFF5A8A2A, 4), anchor, tip);
        DrawPerson(dc, tip.X, tip.Y + 52, 1, 0, Pose.Hang, Khaki, 0xFF6A4A2A, Helmet, 1.1);
        Text(dc, "SWING THE VINES, SWIM THE RIVER, CLIMB THE HILL - AND RESCUE THE PHOTOGRAPHER FROM THE POACHERS", new Point(cx, 400), 19, White, center: true);
        (string Name, string Note, Action<double> Draw)[] legend =
        [
            ("THE VINES", "LET GO AT THE RIGHT TIME", x => { dc.DrawLine(Pen(0xFF5A8A2A, 3), new Point(x - 10, 430), new Point(x + 10, 500)); DrawMonkey(dc, x + 4, 470); }),
            ("THE RIVER", "STAB WHEN THE JAWS SHUT", x => DrawCroc(dc, x + 20, 470, (int)(_clock * 1.5) % 2 == 0, false, _clock)),
            ("THE HILL", "JUMP SMALL, DUCK BIG", x => { dc.DrawEllipse(Brush(0xFF8A8A8A), Pen(0xFF4A4A4A, 2), new Point(x - 22, 480), 16, 16); dc.DrawEllipse(Brush(0xFF8A8A8A), Pen(0xFF4A4A4A, 2), new Point(x + 22, 470), 28, 28); }),
            ("THE CAMP", "JUMP THE POACHERS", x => DrawPerson(dc, x, 500 - Math.Max(0, Math.Sin(_clock * 5)) * 20, -1, 0, Pose.Jump, 0xFF6A6A3A, 0xFF4A4A2A, 0xFF3A3A2A, 0.9, poacher: true)),
        ];
        for (int i = 0; i < legend.Length; i++)
        {
            double x = cx - 345 + i * 230;
            legend[i].Draw(x);
            Text(dc, legend[i].Name, new Point(x, 530), 22, 0xFF8AE08A, center: true);
            Text(dc, legend[i].Note, new Point(x, 556), 17, Yellow, center: true);
        }
        if ((int)(_clock * 2) % 2 == 0)
            Text(dc, "PRESS ENTER OR 1 TO PLAY", new Point(cx, 618), 46, Yellow, center: true);
        Text(dc, "ARROWS: RUN, SWIM, DUCK    SPACE: JUMP / LET GO    Z OR X: KNIFE", new Point(cx, 680), 22, Dim, center: true);
        Text(dc, $"P: PAUSE    M: SOUND    EXTRA LIFE EVERY {GameEngine.ExtraLifeEvery}    GAMEPAD: STICK, A, B", new Point(cx, 710), 21, Dim, center: true);
    }

    private void DrawHighScores(DrawingContext dc)
    {
        var g = Engine;
        double cx = Field.Width / 2;
        dc.FillRectangle(Brush(0xC0000000), new Rect(0, 0, Field.Width, Field.Height));
        Text(dc, "GREAT EXPLORERS", new Point(cx, 140), 68, 0xFF8AE08A, center: true);
        Text(dc, "RANK NAME    SCORE   ROUND", new Point(cx, 204), 30, Dim, center: true);
        var entries = g.HighScores.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            bool mine = i == g.NewEntryIndex && (int)(_clock * 4) % 2 == 0;
            Text(dc, $"{i + 1,2}.  {e.Initials}  {e.Score,8}   {e.Stage,4}", new Point(cx, 248 + i * 42), 34, mine ? Red : i == 0 ? Yellow : White, center: true);
        }
        Text(dc, "PRESS ENTER TO PLAY", new Point(cx, 710), 30, White, center: true);
    }

    private void DrawInitials(DrawingContext dc)
    {
        var g = Engine;
        double cx = Field.Width / 2;
        dc.FillRectangle(Brush(0xC0000000), new Rect(0, 0, Field.Width, Field.Height));
        Text(dc, "A NEW HIGH SCORE!", new Point(cx, 200), 64, Yellow, center: true);
        Text(dc, g.Score.ToString(), new Point(cx, 270), 52, White, center: true);
        Text(dc, "ENTER YOUR INITIALS", new Point(cx, 350), 34, Dim, center: true);
        if (g.Initials is { } entry)
        {
            string s = entry.Initials;
            for (int i = 0; i < s.Length; i++)
            {
                bool cur = i == entry.Cursor && (int)(_clock * 3) % 2 == 0;
                Text(dc, s[i].ToString(), new Point(cx - 60 + i * 60, 440), 80, cur ? Red : Yellow, center: true);
                dc.FillRectangle(Brush(i == entry.Cursor ? Red : Dim), new Rect(cx - 82 + i * 60, 480, 44, 4));
            }
        }
        Text(dc, "TYPE, OR UP/DOWN AND SPACE; ENTER WHEN DONE", new Point(cx, 560), 26, White, center: true);
    }
}
