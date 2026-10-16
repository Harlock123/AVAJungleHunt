namespace AVAJungleHunt.Engine;

/// <summary>The controls: left/right (-1, 0, 1), up/down (1 up, -1 down), jump (and let go), and the knife.</summary>
public readonly record struct Controls(int Dx = 0, int Dy = 0, bool Jump = false, bool Fire = false);

/// <summary>
/// The screen and the world: each scene is a long stretch of jungle the screen follows the explorer along, left
/// to right. World x runs along the scene; y is the screen's, growing downwards.
/// </summary>
public static class Field
{
    public const double Step = 1.0 / 60;
    public const double Width = 1024, Height = 768;
    public const double Gravity = 1500;
    /// <summary>How far in from the left of the screen the explorer is kept.</summary>
    public const double Lead = 300;

    // The vines: hanging from the canopy over a swamp.
    public const double Canopy = 70, Swamp = 720, LedgeTop = 540;
    // The river: its surface, and the bed.
    public const double Surface = 300, RiverBed = 720;
    // The camp: flat ground.
    public const double CampGround = 680;

    /// <summary>The hill's slope: the ground rises to the right.</summary>
    public static double HillGround(double x) => 700 - Math.Clamp(x, 0, 2600) * 0.16;
    public const double HillSlope = 0.16;

    public static double Distance(double ax, double ay, double bx, double by) => Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
}

/// <summary>The explorer: on a vine, in the air, in the water, or on his feet.</summary>
public sealed class Explorer
{
    public double X, Y, Vx, Vy, Walk, Safe;
    public bool Alive = true, Grounded, Ducking;
    public int Facing = 1;
    /// <summary>The vine he's holding (or -1), and how far down it his hands are.</summary>
    public int Vine = -1;
    public double Along;
    /// <summary>Air left in his lungs, in the river.</summary>
    public double Air = 100;
    /// <summary>The knife thrust, while it lasts.</summary>
    public double Stab, StabIn;
    public const double Width = 22, Height = 52, DuckHeight = 26;
    public double Tall => Ducking ? DuckHeight : Height;
}

/// <summary>A vine swinging from the canopy: a pendulum, with sometimes a monkey climbing up and down it.</summary>
public sealed class Vine
{
    public double X, Length, Swing, Phase;
    /// <summary>A monkey on it: whether, and how fast it climbs.</summary>
    public bool Monkey;
    public double MonkeyRate;

    public double Rate => Math.Sqrt(Field.Gravity / Length);
    public double AngleAt(double t) => Swing * Math.Sin(Rate * t + Phase);
    public double SpinAt(double t) => Swing * Rate * Math.Cos(Rate * t + Phase);

    /// <summary>A point on the vine a distance down it, at a time.</summary>
    public (double X, double Y) At(double along, double t)
    {
        double a = AngleAt(t);
        return (X + Math.Sin(a) * along, Field.Canopy + Math.Cos(a) * along);
    }

    /// <summary>How far down the vine the monkey is.</summary>
    public double MonkeyAt(double t) => Length * (0.55 + 0.35 * Math.Sin(t * MonkeyRate + Phase * 2));
}

/// <summary>A crocodile, swimming left at its depth, its jaws opening and shutting.</summary>
public sealed class Croc
{
    public double X, Y, Speed, T, Cycle, Open;
    public bool Dead;
    public double DeadT;
    /// <summary>Whether its jaws are open (it bites) or shut (the knife will do it).</summary>
    public bool JawsOpen => !Dead && T % Cycle < Open;
    public const double Length = 110, Thick = 30;
    /// <summary>Its head's middle (it swims left, so its head is on its left).</summary>
    public double HeadX => X - Length / 2 + 18;
}

/// <summary>A boulder bounding down the hill.</summary>
public sealed class Boulder
{
    public double X, Y, Vx, Vy, R, Spin;
    public bool Big, Passed;
}

/// <summary>A poacher at the camp, hopping up and down (and a little to and fro) in the way.</summary>
public sealed class Guard
{
    public double Home, X, Y, Hop, Period, Phase, Pace;
    public bool Passed;
    public const double Width = 30, Height = 56;

    /// <summary>Where it is at a time: its hop, and its pacing.</summary>
    public (double X, double Y) At(double t)
    {
        double s = (t / Period + Phase) % 1;
        double up = Math.Max(0, Math.Sin(s * Math.PI * 2)) * Hop;
        return (Home + Math.Sin(t * 0.9 + Phase * 6) * Pace, Field.CampGround - up);
    }
}

public enum EffectKind { Score, Word, Splash, Burst }

public sealed class Effect
{
    public EffectKind Kind;
    public double X, Y, Age, Life = 0.8, Size = 1;
    public string Text = "";
    public uint Colour = 0xFFFFFFFF;
    public bool Done => Age >= Life;
}
