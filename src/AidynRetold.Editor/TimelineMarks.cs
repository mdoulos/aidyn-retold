using AidynRetold.Core.Actors;
using AidynRetold.Core.Audio;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;
using AidynRetold.Views;

namespace AidynRetold.Editor;

/// <summary>
/// What happens in a clip of an actor's, for the timeline (<see cref="TimelineMark"/>), as the game times it: the requests that
/// lead into the states playing the clip say which keys reach it. A footstep key plays at its share of the state's length in the
/// body's footstep set for its first surface (<c>AudioKeys.Reached</c>), the character's own keys likewise, an emitter that starts
/// on a request starts on the state's first tick and its delay after (<c>ActorParticles.Entered</c>), and an attack's blow lands at
/// the character's strike share of it, or 0.4 for a bow shot or a throw (<c>Battle.AtStrike</c>).
/// </summary>
public static class TimelineMarks
{
    private const int FirstAttack = 12, LastAttack = 18, Shot = 14, Throw = 15;

    public static IReadOnlyList<TimelineMark> For(PluginSet plugins, ActorRecord actor, RecordId scene, ModelInspector.Wearer? wearer, string clip, int length)
    {
        var marks = new List<TimelineMark>();
        var states = actor.States.Index().Where(s => ActorAnimator.AnimationName(s.Item) == clip).Select(s => s.Index).ToHashSet();
        if (states.Count == 0 || length <= 0)
            return marks;
        var requests = actor.States.SelectMany(s => s.Branches).Where(b => states.Contains(b.State) && b.Request != ActorStateMachine.AnyRequest)
            .Select(b => b.Request).ToHashSet();
        if (states.Contains(actor.InitialState))
            requests.Add(0);

        if (wearer?.Entity?.Sounds is { } profile)
        {
            if (plugins.TryGet<SoundsRecord>(RecordId.Decimal(RecordTypes.Sounds, 0)) is { } sounds && profile.Kind >= 0 && profile.Kind < sounds.Kinds.Count
                && sounds.Kinds[profile.Kind] is { Surfaces.Count: > 0 } kind)
            {
                var surface = kind.Surfaces[0];
                foreach (var (key, gait) in new[] { (surface.Sneak, "sneaking"), (surface.Walk, "walking"), (surface.Run, "running") })
                {
                    if (key is not null && requests.Contains(key.Request))
                        marks.Add(new TimelineMark(At(key, length), TimelineMarkKind.Footstep, $"a footstep, {gait}"));
                }
            }
            foreach (var key in profile.Keys.Where(k => requests.Contains(k.Request)))
                marks.Add(new TimelineMark(At(key, length), TimelineMarkKind.Sound,
                    key.Sounds.Count == 0 ? "its weapon's sound" : $"its sound, {string.Join(", ", key.Sounds.Select(s => s.Key))}"));
        }

        if (scene.Namespace == RecordId.RomNamespace
            && plugins.TryGet<EmittersRecord>(RecordId.Hex(RecordTypes.Emitters, Convert.ToInt32(scene.Key, 16))) is { } emitters)
        {
            for (var i = 0; i < emitters.Emitters.Count; i++)
            {
                var e = emitters.Emitters[i];
                if (e.Flags.HasFlag(ParticleEmitterFlags.ActorRequest) && e.Request is { } r && requests.Contains(r))
                    marks.Add(new TimelineMark(Math.Min(1 + e.Delay, length), TimelineMarkKind.Emitter, $"emitter {i} starts, on locator {e.Locator.Index}"));
            }
        }

        var attacks = requests.Where(r => r is >= FirstAttack and <= LastAttack).ToList();
        if (attacks.Count > 0)
        {
            var share = attacks.All(r => r is Shot or Throw) ? EntityBody.DefaultStrike : wearer?.Body.Strike ?? EntityBody.DefaultStrike;
            marks.Add(new TimelineMark((int)(length * share), TimelineMarkKind.Strike, $"the blow lands, {share * 100:0}% through the attack"));
        }
        return marks;
    }

    /// <summary>The tick a key plays at in a state of <paramref name="length"/> ticks, from 1, as <c>AudioKeys.Reached</c> has it.</summary>
    private static int At(SoundKey key, int length)
    {
        var tick = (ushort)(int)(key.Frac / AudioKeys.WholeFrac * length);
        return tick == 0 ? 1 : tick;
    }
}
