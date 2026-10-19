using System;
using UnityEngine;

[Serializable]
public sealed class EchoGenerationSnapshot
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public int generation;
    public float[] policyWeights;
    public float[] sequenceTransitions;
    public int sequencePairCount;
    public string styleJson = "";
    public float pace;
    public float clarity = 1f;

    public EchoGenerationSnapshot Clone()
    {
        return FromJson(JsonUtility.ToJson(this));
    }

    public PlayerStyleData GetStyle()
    {
        PlayerStyleData style = null;
        if (!string.IsNullOrEmpty(styleJson))
        {
            try
            {
                style = JsonUtility.FromJson<PlayerStyleData>(styleJson);
            }
            catch (Exception)
            {
                // A damaged style payload falls back to a neutral snapshot.
            }
        }

        style = style ?? new PlayerStyleData();
        style.Normalize();
        return style;
    }

    public string ToJson()
    {
        Normalize();
        return JsonUtility.ToJson(this);
    }

    public static EchoGenerationSnapshot FromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            EchoGenerationSnapshot snapshot =
                JsonUtility.FromJson<EchoGenerationSnapshot>(json);
            if (snapshot == null) return null;
            snapshot.Normalize();
            return snapshot;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Normalize()
    {
        version = CurrentVersion;
        generation = Mathf.Max(0, generation);
        sequencePairCount = Mathf.Max(0, sequencePairCount);
        pace = IsFinite(pace) ? Mathf.Max(0f, pace) : 0f;
        clarity = IsFinite(clarity) ? Mathf.Clamp01(clarity) : 0f;
        styleJson = styleJson ?? "";
        policyWeights = CloneArray(policyWeights);
        sequenceTransitions = CloneArray(sequenceTransitions);
    }

    private static float[] CloneArray(float[] source)
    {
        if (source == null) return null;
        float[] clone = new float[source.Length];
        Array.Copy(source, clone, source.Length);
        return clone;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
