using System.Text;

namespace ObenSave.Tests;

/// <summary>Save-like data for the tests, shaped like what the game writes.</summary>
internal static class SampleSaves
{
    public static readonly byte[] Bom = { 0xEF, 0xBB, 0xBF };

    /// <summary>A minified FullSerializer document like a level's .tnmt, big enough to compress well.</summary>
    public static string LevelJson(int objects = 400)
    {
        var json = new StringBuilder("{\"SaveName\":\"Slot1\",\"LevelName\":\"Interior Player Tenement\",\"NewlevelEntrypoint\":\"NONE\",");
        json.Append("\"CharacterName\":\"Esko_Virtanen\",\"Date\":\"2026-10-28T12:00:00.0000000+01:00\",\"ApplicationVersion\":\"0.5.13\",\"Data\":[");
        for (int i = 0; i < objects; i++)
        {
            if (i > 0)
                json.Append(',');
            json.Append("{\"GUID\":\"Door").Append(i).Append("\",\"Data\":{\"$type\":\"Door\",\"locked\":false,\"opened\":")
                .Append(i % 2 == 0 ? "true" : "false").Append(",\"position\":{\"x\":").Append(i * 0.25).Append(",\"y\":1.5,\"z\":-3.75}}}");
        }
        return json.Append("]}").ToString();
    }

    /// <summary>The bytes the game's writer produces: the text in UTF-8 with a byte order mark.</summary>
    public static byte[] FileBytes(string text, bool bom = true)
    {
        byte[] body = new UTF8Encoding(false).GetBytes(text);
        if (!bom)
            return body;
        var file = new byte[Bom.Length + body.Length];
        Bom.CopyTo(file, 0);
        body.CopyTo(file, Bom.Length);
        return file;
    }
}
