using UnityEngine;

public static class AIModelWeightRules
{
    public static bool TrySanitize(float[] source, int expectedLength,
        float minimum, float maximum, out float[] sanitized)
    {
        sanitized = null;
        if (source == null || source.Length != expectedLength) return false;

        float[] copy = new float[expectedLength];
        for (int i = 0; i < source.Length; i++)
        {
            float value = source[i];
            if (float.IsNaN(value) || float.IsInfinity(value)) return false;
            copy[i] = Mathf.Clamp(value, minimum, maximum);
        }

        sanitized = copy;
        return true;
    }
}
