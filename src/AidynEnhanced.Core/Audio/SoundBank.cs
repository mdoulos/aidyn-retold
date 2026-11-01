using AidynEnhanced.Plugins;
using AidynEnhanced.Plugins.Records;

namespace AidynEnhanced.Core.Audio;

/// <summary>
/// The sequences played so far, loaded from the plugins as they're first asked for and kept, with the samples they
/// share. A sequence no plugin has, or whose assets are missing, plays nothing. A recording in a sequence's place takes a few
/// hundred milliseconds to read, so it's begun on another thread as what might play it loads (<see cref="Warm"/>).
/// </summary>
public sealed class SoundBank(PluginSet plugins)
{
    private readonly Lock _sync = new();
    private readonly Dictionary<RecordId, Sequence?> _sequences = [];
    private readonly Dictionary<RecordId, Task<Sequence?>> _warming = [];
    private readonly Dictionary<RecordId, short[]> _samples = [];

    /// <summary>A sequence that couldn't be loaded, and why: for the log.</summary>
    public event Action<RecordId, string>? Missing;

    /// <summary>Puts a sequence made in code in place of the plugins' own, as tests do.</summary>
    public void Add(RecordId id, Sequence sequence)
    {
        lock (_sync)
            _sequences[id] = sequence;
    }

    /// <summary>
    /// Begins loading a sequence on another thread if a recording plays in its place (<see cref="Sequence.Recording"/>), so it's
    /// ready by the time it's asked for. The original's sequences are small and load as they're asked for.
    /// </summary>
    public void Warm(RecordId id)
    {
        lock (_sync)
        {
            if (_sequences.ContainsKey(id) || _warming.ContainsKey(id) || plugins.FindAsset(id, "wav") is null)
                return;
            _warming[id] = Task.Run(() => Load(id));
        }
    }

    public Sequence? Get(RecordId id)
    {
        Task<Sequence?>? warming;
        lock (_sync)
        {
            if (_sequences.TryGetValue(id, out var sequence))
                return sequence;
            _warming.Remove(id, out warming);
        }
        // Waits for a recording still loading; the original's sequences load here.
        var loaded = warming?.Result ?? Load(id);
        lock (_sync)
            return _sequences[id] = loaded;
    }

    private Sequence? Load(RecordId id)
    {
        try
        {
            Sequence? sequence;
            if (plugins.FindAsset(id, "wav") is not null)
                sequence = Sequence.Load(plugins, id);
            else
            {
                if (plugins.TryGet<SequenceRecord>(id) is null)
                    sequence = null;
                else
                    lock (_samples)
                        sequence = Sequence.Load(plugins, id, _samples);
            }
            if (sequence is null)
                Missing?.Invoke(id, "no plugin has it");
            return sequence;
        }
        catch (Exception e) when (e is IOException or InvalidDataException)
        {
            Missing?.Invoke(id, e.Message);
            return null;
        }
    }
}
