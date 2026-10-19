using System;
using System.Globalization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using UnityEngine;

/// <summary>Validates the existing identity JSON before any Normalize call.</summary>
public static class EchoIdentityExternalPayload
{
    public const int MaximumBytes = 16 * 1024;
    public const int MaximumIdentifierLength = 128;

    public static bool TryRead(string json, out ActiveEchoIdentity identity,
        out string error)
    {
        identity = null;
        error = "PAYLOAD_INVALID";
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            if (json.Length > MaximumBytes)
            {
                error = "PAYLOAD_TOO_LARGE";
                return false;
            }
            byte[] bytes = new UTF8Encoding(false, true).GetBytes(json);
            if (bytes.Length > MaximumBytes)
            {
                error = "PAYLOAD_TOO_LARGE";
                return false;
            }

            // The framework reader checks JSON syntax and exposes raw types.
            // JsonUtility alone supplies missing field defaults and Normalize
            // upgrades versions, neither of which is safe at this boundary.
            var quotas = new XmlDictionaryReaderQuotas
            {
                MaxDepth = 12,
                MaxStringContentLength = MaximumBytes,
                MaxArrayLength = MaximumBytes,
                MaxBytesPerRead = MaximumBytes,
                MaxNameTableCharCount = MaximumBytes
            };
            var document = new XmlDocument { XmlResolver = null };
            using (XmlDictionaryReader reader =
                   JsonReaderWriterFactory.CreateJsonReader(bytes, quotas))
                document.Load(reader);
            XmlElement root = document.DocumentElement;
            RequireType(root, "object");
            RequireVersion(root, ActiveEchoIdentity.CurrentVersion);
            int generation = Integer(root, "generation", 1);
            string id = Identifier(root, "identityId");
            string parent = Identifier(root, "parentIdentityId", true);
            Require(generation == 1 ? parent.Length == 0 : parent.Length > 0);
            Integer(root, "sourceRunSequence", 0);
            Integer(root, "sequencePairCount", 0);
            Number(root, "pace", float.Epsilon, float.MaxValue);
            Number(root, "sourceCourseDuration", 0, float.MaxValue);
            Number(root, "clarity", 0, 1);
            Array(root, "policyWeights", AIShadowPolicy.ActionCount
                * AIShadowPolicy.FeatureCount, -4, 4);
            Array(root, "sequenceTransitions", AIShadowSequencePolicy.ActionCount
                * AIShadowSequencePolicy.ActionCount, 0, float.MaxValue);

            XmlElement style = Field(root, "style", "object");
            RequireVersion(style, PlayerStyleData.CurrentVersion);
            foreach (string name in new[] { "aggressiveness", "slideFrequency",
                         "slideOpportunitySuccess", "rhythmStability", "recoveryStyle" })
                Number(style, name, 0, 1);
            Number(style, "jumpTiming", -1, 1);
            Number(style, "lanePreference", -1, 1);
            foreach (string name in new[] { "aggressivenessSamples", "jumpTimingSamples",
                         "verticalActionSamples", "jumpActionSamples", "slideActionSamples",
                         "slideOpportunitySamples", "laneSamples", "rhythmSamples",
                         "recoverySamples" })
                Integer(style, name, 0);
            long verticalActions = (long)Integer(style, "jumpActionSamples", 0)
                                   + Integer(style, "slideActionSamples", 0);
            Require(verticalActions <= int.MaxValue
                    && Integer(style, "verticalActionSamples", 0) >= verticalActions);

            XmlElement memory = Field(root, "memoryContract", "object");
            RequireVersion(memory, EchoMemoryContract.CurrentVersion);
            Identifier(memory, "contractId");
            Require(string.Equals(Identifier(memory, "identityId"), id,
                StringComparison.Ordinal));
            Require(Integer(memory, "preferredLane", 0) <= 2);
            double confidence = Number(memory, "confidence", 0, 1);
            int evidence = Integer(memory, "evidenceCount", 0);
            if (evidence < 3 || (float)confidence < EchoMemoryContract.PreciseDescriptionConfidence)
            {
                error = "IDENTITY_NOT_CHALLENGE_READY";
                return false;
            }

            // Still only one identity model and one serializer; the XML reader
            // above performs validation, not identity conversion or persistence.
            identity = JsonUtility.FromJson<ActiveEchoIdentity>(json);
            Require(identity != null && !identity.RequiresRouteCalibration);
            error = "";
            return true;
        }
        catch (UnsupportedVersionException)
        {
            error = "PAYLOAD_VERSION_UNSUPPORTED";
        }
        catch (Exception)
        {
            // External input must never break local gameplay or save recovery.
        }
        identity = null;
        return false;
    }

    public static bool IsIdentifier(string value, bool allowEmpty = false)
    {
        if (value == null || value.Length > MaximumIdentifierLength) return false;
        if (value.Length == 0) return allowEmpty;
        foreach (char character in value)
        {
            bool safe = character >= 'a' && character <= 'z'
                        || character >= 'A' && character <= 'Z'
                        || character >= '0' && character <= '9'
                        || character == '-' || character == '_';
            if (!safe) return false;
        }
        return true;
    }

    private static XmlElement Field(XmlElement parent, string name, string type)
    {
        XmlElement found = null;
        foreach (XmlNode child in parent.ChildNodes)
        {
            if (!(child is XmlElement element) || element.LocalName != name) continue;
            Require(found == null);
            found = element;
        }
        RequireType(found, type);
        return found;
    }

    private static void RequireVersion(XmlElement parent, int expected)
    {
        if (Integer(parent, "version", 0) != expected)
            throw new UnsupportedVersionException();
    }

    private static string Identifier(XmlElement parent, string name, bool allowEmpty = false)
    {
        string value = Field(parent, name, "string").InnerText;
        Require(IsIdentifier(value, allowEmpty));
        return value;
    }

    private static int Integer(XmlElement parent, string name, int minimum)
    {
        double value = Number(parent, name, minimum, int.MaxValue);
        Require(Math.Truncate(value) == value);
        return (int)value;
    }

    private static double Number(XmlElement parent, string name, double minimum, double maximum)
    {
        return NumericValue(Field(parent, name, "number"), minimum, maximum);
    }

    private static double NumericValue(XmlElement element, double minimum, double maximum)
    {
        RequireType(element, "number");
        Require(double.TryParse(element.InnerText, NumberStyles.Float,
            CultureInfo.InvariantCulture, out double value));
        Require(!double.IsNaN(value) && !double.IsInfinity(value)
                && value >= minimum && value <= maximum);
        return value;
    }

    private static void Array(XmlElement parent, string name, int length,
        double minimum, double maximum)
    {
        XmlElement array = Field(parent, name, "array");
        int count = 0;
        foreach (XmlNode child in array.ChildNodes)
        {
            Require(child is XmlElement);
            NumericValue((XmlElement)child, minimum, maximum);
            count++;
        }
        Require(count == length);
    }

    private static void RequireType(XmlElement element, string type)
    {
        Require(element != null && element.GetAttribute("type") == type);
    }

    private static void Require(bool valid)
    {
        if (!valid) throw new FormatException("Invalid external identity.");
    }

    private sealed class UnsupportedVersionException : Exception { }
}
