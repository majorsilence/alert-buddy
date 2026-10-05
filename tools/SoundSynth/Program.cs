// Peak level is capped at 0.5 of full scale, about -6 dBFS (PLAN.md section 8.8). 16-bit mono PCM WAV, 22.05 kHz.
const int Rate = 22050;
const double Peak = 0.5;

var outDir = args.Length > 0 ? args[0] : "sounds";
Directory.CreateDirectory (outDir);

Write ("boop", Boop (1.0));
Write ("warning", Warning (1.0));
Write ("alarm", Alarm ());
Write ("allclear", AllClear ());
Write ("cheer", Cheer ());
Write ("practice", Warning (1.0), Peak * 0.4);
Write ("code3", Code3 ());
Write ("marchtime", MarchTime ());
Write ("continuous", Continuous ());
Write ("voice", VoiceChime ());

void Write (string name, double[] samples, double peak = Peak)
{
    var max = samples.Max (Math.Abs);
    var scale = max == 0 ? 0 : peak / max;
    var path = Path.Combine (outDir, name + ".wav");
    using var file = File.Create (path);
    using var w = new BinaryWriter (file);
    var bytes = samples.Length * 2;
    w.Write ("RIFF"u8.ToArray ());
    w.Write (36 + bytes);
    w.Write ("WAVEfmt "u8.ToArray ());
    w.Write (16);
    w.Write ((short)1);
    w.Write ((short)1);
    w.Write (Rate);
    w.Write (Rate * 2);
    w.Write ((short)2);
    w.Write ((short)16);
    w.Write ("data"u8.ToArray ());
    w.Write (bytes);
    foreach (var s in samples)
        w.Write ((short)Math.Round (s * scale * short.MaxValue));
    Console.WriteLine ($"{path}  {samples.Length / (double)Rate:0.00}s");
}

// A note with a soft attack and an exponential decay, so nothing clicks.
double[] Note (double hz, double seconds, double decay = 5)
{
    var n = (int)(seconds * Rate);
    var data = new double[n];
    for (var i = 0; i < n; i++) {
        var t = i / (double)Rate;
        var attack = Math.Min (1, t / 0.008);
        var tail = Math.Min (1, (seconds - t) / 0.02);
        data[i] = Math.Sin (2 * Math.PI * hz * t) * attack * tail * Math.Exp (-decay * t / seconds);
    }
    return data;
}

double[] Join (params double[][] parts) => parts.SelectMany (p => p).ToArray ();
double[] Silence (double seconds) => new double[(int)(seconds * Rate)];

double[] Boop (double level) => Note (330, 0.12, 6).Select (s => s * level).ToArray ();

double[] Warning (double level) => Join (Note (523.25, 0.16), Silence (0.03), Note (659.25, 0.26)).Select (s => s * level).ToArray ();

double[] AllClear () => Join (Note (523.25, 0.16), Note (659.25, 0.16), Note (783.99, 0.36));

double[] Cheer () => Join (Note (523.25, 0.09), Note (659.25, 0.09), Note (783.99, 0.09), Note (1046.5, 0.3));

// One rising "whoop" that fades in and out, so the loop has a breath between whoops and is firm, not harsh.
double[] Alarm ()
{
    const double seconds = 1.1;
    var n = (int)(seconds * Rate);
    var data = new double[n];
    var phase = 0.0;
    for (var i = 0; i < n; i++) {
        var t = i / (double)Rate;
        var progress = t / seconds;
        var hz = 520 + 480 * progress * progress;
        phase += 2 * Math.PI * hz / Rate;
        var envelope = Math.Min (1, t / 0.05) * Math.Min (1, (seconds - t) / 0.12);
        data[i] = (Math.Sin (phase) + 0.3 * Math.Sin (2 * phase)) * envelope;
    }
    return data;
}

// A steady tone with a touch of second harmonic, ramped over 5 ms at each end so a burst does not click.
double[] Beep (double hz, double seconds)
{
    var n = (int)(seconds * Rate);
    var data = new double[n];
    for (var i = 0; i < n; i++) {
        var t = i / (double)Rate;
        var ramp = Math.Min (Math.Min (1, t / 0.005), Math.Min (1, (seconds - t) / 0.005));
        data[i] = (Math.Sin (2 * Math.PI * hz * t) + 0.3 * Math.Sin (4 * Math.PI * hz * t)) * ramp;
    }
    return data;
}

// Code 3, the temporal-3 pattern (ISO 8201): three 0.5 s tones with 0.5 s gaps, then a 1.5 s rest. One cycle, looped.
double[] Code3 ()
{
    var burst = Join (Beep (800, 0.5), Silence (0.5));
    return Join (burst, burst, burst, Silence (1.0));
}

// March time: 120 beats a minute, each a 0.25 s tone and a 0.25 s gap. Two seconds, looped.
double[] MarchTime ()
{
    var beat = Join (Beep (800, 0.25), Silence (0.25));
    return Join (beat, beat, beat, beat);
}

// Continuous: a whole number of cycles (600 Hz for one second), so the loop point is seamless.
double[] Continuous ()
{
    var data = new double[Rate];
    for (var i = 0; i < Rate; i++)
        data[i] = Math.Sin (2 * Math.PI * 600 * i / (double)Rate);
    return data;
}

// The two-note attention chime that opens a voice evacuation message; the spoken words come from the device's voice. Then a rest, looped.
double[] VoiceChime () => Join (Note (880, 0.45, 3), Note (659.25, 0.7, 3), Silence (0.4));
