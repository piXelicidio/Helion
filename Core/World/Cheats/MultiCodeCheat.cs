using System;

namespace Helion.World.Cheats;

public class MultiCodeCheat : ICheat
{
    private readonly string[] m_codes;

    public string CheatOn { get; }
    public string CheatOff { get; }
    public string? ConsoleCommand { get; }
    public CheatType CheatType { get; }
    public bool Activated { get; set; }
    public bool IsToggleCheat { get; private set; }
    public bool ClearTypedCheatString { get; private set; }

    public MultiCodeCheat(string on, string off, string[] codes, string consoleCommand, CheatType cheatType, bool canToggle = true,
        bool clearTypedCheatString = true)
    {
        CheatOn = on;
        CheatOff = off;
        ConsoleCommand = consoleCommand;
        m_codes = codes;
        CheatType = cheatType;
        IsToggleCheat = canToggle;
        ClearTypedCheatString = clearTypedCheatString;
    }

    public void SetCode(string code, int index = 0)
    {
        if (index < 0 || index >= m_codes.Length)
            return;

        m_codes[index] = code;
    }

    public bool IsMatch(ReadOnlySpan<char> str)
    {
        for (int i = 0; i < m_codes.Length; i++)
        {
            if (m_codes[i].AsSpan().Equals(str, StringComparison.InvariantCultureIgnoreCase))
                return true;
        }

        return false;
    }

    public bool PartialMatch(ReadOnlySpan<char> str)
    {
        for (int i = 0; i < m_codes.Length; i++)
        {
            if (m_codes[i].AsSpan().StartsWith(str, StringComparison.InvariantCultureIgnoreCase))
                return true;
        }

        return false;
    }
}
