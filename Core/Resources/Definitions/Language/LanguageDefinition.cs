﻿using Helion.Util.Parser;
using Helion.World.Entities.Players;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Helion.Resources.Definitions.Language
{
    public class LanguageDefinition
    {
        public CultureInfo CultureInfo { get; set; } = CultureInfo.CurrentCulture;

        private readonly Dictionary<string, string> m_lookup = new(StringComparer.OrdinalIgnoreCase);

        public void Parse(string data)
        {
            data = GetCurrentLanguageSection(data);

            SimpleParser parser = new SimpleParser();
            parser.Parse(data);
            StringBuilder sb = new StringBuilder();

            while (!parser.IsDone())
            {
                string key = parser.ConsumeString();
                parser.ConsumeString("=");

                sb.Clear();
                do
                {
                    sb.Append(parser.ConsumeString().Replace("\\n", "\n"));
                } while (!parser.Peek(';'));

                parser.ConsumeString(";");

                m_lookup[key] = sb.ToString();
            }
        }

        public void SetValue(string key, string value) =>
            m_lookup[key] = value;

        private string GetCurrentLanguageSection(string data)
        {
            Regex currentLanguage = new Regex(string.Format("\\[{0}\\w?(\\s+default)?]", CultureInfo.TwoLetterISOLanguageName));
            Regex defaultLanguage = new Regex("\\[\\w+\\s+default]");
            Regex anyLanguage = new Regex("\\[\\w+(\\s+default)?]");

            Match m = currentLanguage.Match(data);
            if (m.Success)
                return GetLanguageSection();

            m = defaultLanguage.Match(data);
            if (m.Success)
                return GetLanguageSection();

            string GetLanguageSection()
            {
                int startIndex = m.Index + m.Length;
                int endIndex = data.Length;
                m = anyLanguage.Match(data, startIndex);

                if (m.Success)
                    endIndex = m.Index;

                return data[startIndex..endIndex];
            }

            return data;
        }

        public string[] GetMessages(string message)
        {
            if (message.Length > 0 && message[0] == '$')
                return LookupMessage(message[1..]).Split(new string[] { "\n", "\r\n" }, StringSplitOptions.None);

            return new string[] { message };
        }

        public string GetMessage(string message)
        {
            if (message.Length > 0 && message[0] == '$')
                return LookupMessage(message[1..]);

            return message;
        }

        public string GetMessage(Player player, Player? other, string message)
        {
            if (message.Length > 0 && message[0] == '$')
            {
                message = LookupMessage(message[1..]);
                return AddMessageParams(player, other, message);
            }

            return message;
        }

        public bool GetKeyByValue(string text, out string? key)
        {
            const int Length = 32;
            key = null;
            if (text.Length > Length)
                text = text.Substring(0, Length);

            foreach (var data in m_lookup)
            {
                if (data.Value.Length < Length)
                    continue;

                if (data.Value.StartsWith(text, StringComparison.OrdinalIgnoreCase))
                {
                    key = data.Key;
                    return true;
                }
            }

            return false;
        }

        private static string AddMessageParams(Player player, Player? other, string message)
        {
            message = message.Replace("%o", player.Info.Name, StringComparison.OrdinalIgnoreCase);
            message = message.Replace("%g", player.Info.GetGenderSubject(), StringComparison.OrdinalIgnoreCase);
            message = message.Replace("%h", player.Info.GetGenderObject(), StringComparison.OrdinalIgnoreCase);
            if (other != null)
                message = message.Replace("%k", player.Info.Name, StringComparison.OrdinalIgnoreCase);
            return message;
        }

        private string LookupMessage(string message)
        {
            if (m_lookup.TryGetValue(message, out string? translatedMessage))
                return translatedMessage;

            return string.Empty;
        }
    }
}
