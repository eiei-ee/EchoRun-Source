using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Regenerates the cloud test fixture using the production identity serializer.</summary>
public static class AsyncEchoFixtureExport
{
    [MenuItem("Tools/Async Echo/Export Cloud Test Identity")]
    public static void Export()
    {
        ActiveEchoIdentity identity = SingleContractValidationIdentity.Create();
        identity.sourceRunSequence = 7;
        identity.identityId = ActiveEchoIdentity.CreateIdentityId(identity);
        identity.memoryContract.identityId = identity.identityId;
        string json = identity.ToJson();
        if (!ActiveEchoIdentity.TryFromExternalJson(json, out _, out string error))
            throw new System.InvalidOperationException("Invalid cloud fixture: " + error);

        string path = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../Tools/CloudFunctions/echo/test/fixtures/identity-v1.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, json + "\n", new UTF8Encoding(false));
        Debug.Log("Async Echo fixture exported with ActiveEchoIdentity.ToJson: " + path);
    }
}
