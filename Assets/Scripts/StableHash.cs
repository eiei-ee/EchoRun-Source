using System.Text;

public static class StableHash
{
    public static string ComputeHex(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value ?? "");
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;
        for (int i = 0; i < bytes.Length; i++)
        {
            hash ^= bytes[i];
            hash = unchecked(hash * prime);
        }
        return hash.ToString("X16");
    }
}
