using System;
using System.Collections.Generic;
using System.Text;

/// <summary>Share routing only. Inviter is a lookup key, never authentication.</summary>
public sealed class AsyncEchoInvitation
{
    public const int SupportedRulesVersion = 1;
    public const int MaximumQueryBytes = 1024;
    public string InviterOpenid { get; }
    public string IdentityId { get; }
    public int RulesVersion { get; }
    public string Key => InviterOpenid + ":" + IdentityId + ":" + RulesVersion;

    public AsyncEchoInvitation(string inviterOpenid, string identityId, int rulesVersion)
    { InviterOpenid = inviterOpenid; IdentityId = identityId; RulesVersion = rulesVersion; }

    public string ToQuery() => "inviter=" + Uri.EscapeDataString(InviterOpenid)
        + "&shadow=" + Uri.EscapeDataString(IdentityId) + "&rules=" + RulesVersion;

    public static bool IsValidId(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 128) return false;
        foreach (char c in value)
            if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')
                || (c >= '0' && c <= '9') || c == '-' || c == '_')) return false;
        return true;
    }

    /// <summary>Success with null means an ordinary launch with no invitation.</summary>
    public static bool TryParse(string query, out AsyncEchoInvitation invitation, out string error)
    {
        invitation = null; error = null; query = query ?? "";
        if (Encoding.UTF8.GetByteCount(query) > MaximumQueryBytes)
        { error = "QUERY_TOO_LARGE"; return false; }
        if (query.StartsWith("?", StringComparison.Ordinal)) query = query.Substring(1);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string pair in query.Split('&'))
        {
            if (pair.Length == 0) continue;
            int separator = pair.IndexOf('=');
            string key, value;
            if (!TryDecode(separator < 0 ? pair : pair.Substring(0, separator), out key)
                || !TryDecode(separator < 0 ? "" : pair.Substring(separator + 1), out value))
            { error = "INVALID_QUERY_ENCODING"; return false; }
            bool required = key == "inviter" || key == "shadow" || key == "rules";
            if (required && values.ContainsKey(key))
            { error = "DUPLICATE_QUERY_FIELD"; return false; }
            values[key] = value;
        }
        return TryParse(values, out invitation, out error);
    }

    public static bool TryParse(IDictionary<string, string> query,
        out AsyncEchoInvitation invitation, out string error)
    {
        invitation = null; error = null;
        if (query == null || (!query.ContainsKey("inviter")
            && !query.ContainsKey("shadow") && !query.ContainsKey("rules"))) return true;
        int size = 0;
        foreach (var pair in query)
        {
            size += Encoding.UTF8.GetByteCount(pair.Key ?? "")
                + Encoding.UTF8.GetByteCount(pair.Value ?? "") + 2;
            if (size > MaximumQueryBytes) { error = "QUERY_TOO_LARGE"; return false; }
        }
        string inviter, shadow, rules;
        if (!query.TryGetValue("inviter", out inviter)
            || !query.TryGetValue("shadow", out shadow)
            || !query.TryGetValue("rules", out rules)
            || !IsValidId(inviter) || !IsValidId(shadow))
        { error = "INVALID_INVITATION"; return false; }
        if (rules != "1") { error = "RULES_UNSUPPORTED"; return false; }
        invitation = new AsyncEchoInvitation(inviter, shadow, SupportedRulesVersion);
        return true;
    }

    private static bool TryDecode(string value, out string decoded)
    {
        decoded = null;
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] != '%') continue;
            if (i + 2 >= value.Length || !IsHex(value[i + 1]) || !IsHex(value[i + 2]))
                return false;
            i += 2;
        }
        try { decoded = Uri.UnescapeDataString(value.Replace("+", " ")); return true; }
        catch (UriFormatException) { return false; }
    }
    private static bool IsHex(char c) => (c >= '0' && c <= '9')
        || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
}
