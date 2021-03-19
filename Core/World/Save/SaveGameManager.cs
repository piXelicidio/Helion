﻿using Helion.Util.Configs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Helion.Util.Extensions;
using Helion.Models;
using Helion.Resources.Archives.Collection;
using Helion.World.Util;

namespace Helion.World.Save
{
    public class SaveGameManager
    {
        private readonly Config m_config;

        public SaveGameManager(Config config)
        {
            m_config = config;
        }

        public string WriteNewSaveGame(IWorld world, string title) =>
            WriteSaveGame(world, title, null);

        public string WriteSaveGame(IWorld world, string title, SaveGame? existingSave)
        {
            string filename = existingSave?.FileName ?? GetNewSaveName();
            SaveGame.WriteSaveGame(world, title, filename);
            return filename;
        }

        public List<SaveGame> GetSortedSaveGames(ArchiveCollection archiveCollection)
        {
            var saveGames = GetSaveGames();
            var matchingGames = GetMatchingSaveGames(saveGames, archiveCollection);
            var nonMatchingGames = saveGames.Except(matchingGames);
            return matchingGames.Union(nonMatchingGames).ToList();
        }

        public IEnumerable<SaveGame> GetMatchingSaveGames(IEnumerable<SaveGame> saveGames, 
            ArchiveCollection archiveCollection)
        {
            return saveGames.Where(x => x.Model != null &&
                ModelVerification.VerifyModelFiles(x.Model.Files, archiveCollection, null));
        }

        public List<SaveGame> GetSaveGames()
        {
            return Directory.GetFiles(Directory.GetCurrentDirectory(), "*.hsg")
                .Select(f => new SaveGame(f))
                .OrderByDescending(f => f.Model?.Date)
                .ToList();
        }

        private string GetNewSaveName()
        {
            List<string> files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.hsg")
                .Select(Path.GetFileName)
                .WhereNotNull()
                .ToList();

            int number = 0;
            while (true)
            {
                string name = GetSaveName(number);
                if (files.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    number++;
                else
                    return name;
            }
        }

        private static string GetSaveName(int number) => $"savegame{number}.hsg";
    }
}
