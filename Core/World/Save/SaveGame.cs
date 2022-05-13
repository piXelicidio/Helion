using Helion.Models;
using Helion.Resources.Definitions.MapInfo;
using Newtonsoft.Json;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Helion.World.Save;

public class SaveGame
{
    private static readonly string SaveDataFile = "save.json";
    private static readonly string WorldDataFile = "world.json";

    private static readonly JsonSerializerSettings DefaultSerializerSettings = new JsonSerializerSettings
    {
        NullValueHandling = NullValueHandling.Ignore,
        MissingMemberHandling = MissingMemberHandling.Ignore,
        TypeNameHandling = TypeNameHandling.Auto,
        DefaultValueHandling = DefaultValueHandling.Ignore
    };

    public readonly SaveGameModel? Model;

    public readonly string FileName;

    public SaveGame(string filename)
    {
        FileName = filename;

        try
        {
            using ZipArchive zipArchive = ZipFile.Open(FileName, ZipArchiveMode.Read);
            ZipArchiveEntry? saveDataEntry = zipArchive.Entries.FirstOrDefault(x => x.Name.Equals(SaveDataFile));
            if (saveDataEntry == null)
                return;

            Model = JsonConvert.DeserializeObject<SaveGameModel>(GetEntryString(saveDataEntry), DefaultSerializerSettings);
        }
        catch
        {
            // Corrupt zip or bad serialize
        }
    }

    public WorldModel? ReadWorldModel()
    {
        if (Model == null)
            return null;

        try
        {
            using ZipArchive zipArchive = ZipFile.Open(FileName, ZipArchiveMode.Read);
            ZipArchiveEntry? entry = zipArchive.Entries.FirstOrDefault(x => x.Name.Equals(Model.WorldFile));
            if (entry == null)
                return null;

            return JsonConvert.DeserializeObject<WorldModel>(GetEntryString(entry), DefaultSerializerSettings);
        }
        catch
        {
            return null;
        }
    }

    private static string GetEntryString(ZipArchiveEntry entry)
    {
        byte[] data = new byte[entry.Length];
        using Stream stream = entry.Open();
        int totalRead = 0;
        while (totalRead < data.Length)
            totalRead += stream.Read(data, totalRead, (int)entry.Length - totalRead);
        return Encoding.UTF8.GetString(data);
    }

    public static WorldModel WriteSaveGame(IWorld world, string title, string filename)
    {
        SaveGameModel saveGameModel = new(world.GetGameFilesModel(), title, world.MapInfo.GetMapNameWithPrefix(world.ArchiveCollection),
            DateTime.Now, "world.json");

        if (File.Exists(filename))
            File.Delete(filename);

        using ZipArchive zipArchive = ZipFile.Open(filename, ZipArchiveMode.Create);
        ZipArchiveEntry entry = zipArchive.CreateEntry(SaveDataFile);
        using (Stream stream = entry.Open())
            stream.Write(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(saveGameModel, DefaultSerializerSettings)));

        WorldModel worldModel = world.ToWorldModel();
        entry = zipArchive.CreateEntry(WorldDataFile);
        using (Stream stream = entry.Open())
            stream.Write(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(worldModel, DefaultSerializerSettings)));
        return worldModel;
    }
}
