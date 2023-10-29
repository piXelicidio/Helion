﻿using Helion.Geometry;
using Helion.Graphics.Fonts;
using Helion.Render.Common.Enums;
using Helion.Render.Common.Renderers;
using Helion.Render.OpenGL.Texture.Fonts;
using Helion.Resources.Archives.Collection;
using Helion.Resources.IWad;
using Helion.Util;
using Helion.Util.Timing;
using Helion.Window;
using Helion.Window.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Helion.Layer.IwadSelection;

public class IwadSelectionLayer : IGameLayer
{
    private struct IwadData
    {
        public string FullPath;
        public string Name;
        public IWadInfo IWadInfo;

        public IwadData(string fullPath, string name, IWadInfo iWadInfo)
        {
            FullPath = fullPath;
            Name = name;
            IWadInfo = iWadInfo;
        }
    }

    public event EventHandler<string>? OnIwadSelected;

    private static readonly string ConsoleFont = "Console";
    private readonly ArchiveCollection m_archiveCollection;
    private readonly List<IwadData> m_iwadData = new();
    private int m_selectedIndex;
    private IwadData? m_loading;

    public IwadSelectionLayer(ArchiveCollection archiveCollection)
    {
        m_archiveCollection = archiveCollection;
        IWadLocator iwadLocator = new(new[] { Directory.GetCurrentDirectory() });
        var iwadData = iwadLocator.Locate().OrderBy(x => Path.GetFileName(x.Item1));
        foreach (var data in iwadData)
            m_iwadData.Add(new(data.Item1, $"{Path.GetFileName(data.Item1)}: {data.Item2.Title}", data.Item2));
    }

    public void Render(IRenderableSurfaceContext ctx, IHudRenderContext hud)
    {
        int y = 0;
        hud.Text("Select which IWAD to run:", ConsoleFont, 20, (0, y), out var dim, both: Align.Center);
        y += dim.Height + 16;
        int maxWidth = 0;
        int selectedY = 0;

        const int FontSize = 20;

        foreach (var data in m_iwadData)
        {
            var measuredDim = hud.MeasureText(data.Name, ConsoleFont, FontSize);
            if (measuredDim.Width > maxWidth)
                maxWidth = measuredDim.Width;
        }

        for (int i = 0; i < m_iwadData.Count; i++)
        {
            var data = m_iwadData[i];
            var text = data.Name;
            var currentDim = hud.MeasureText(text, ConsoleFont, FontSize);
            hud.Text(text, ConsoleFont, FontSize, (-((maxWidth - currentDim.Width)/2), y), out dim, both: Align.Center);
            if (i == m_selectedIndex)
                selectedY = y;
            y += dim.Height + 8;
        }

        hud.Text("* ", ConsoleFont, FontSize, (-maxWidth / 2 - 8, selectedY), both: Align.Center);

        if (m_loading != null)
            hud.Text($"Loading {m_loading.Value.Name}...", ConsoleFont, 20, (0, y + 24), out dim, both: Align.Center);
    }

    public void HandleInput(IConsumableInput input)
    {
        if (m_loading != null)
            return;

        if (input.ConsumeKeyPressed(Key.Enter) && m_selectedIndex < m_iwadData.Count)
        {
            m_loading = m_iwadData[m_selectedIndex];
            OnIwadSelected?.Invoke(this, m_loading.Value.FullPath);
        }

        if (input.ConsumeKeyPressed(Key.Down))
            m_selectedIndex = ++m_selectedIndex % m_iwadData.Count;
        if (input.ConsumeKeyPressed(Key.Up))
            m_selectedIndex = Math.Max(--m_selectedIndex, 0);
    }

    public void RunLogic(TickerInfo tickerInfo)
    {
        
    }

    public void Dispose()
    {
        
    }

    private Font GetFontOrDefault(string name)
    {
        var font = m_archiveCollection.GetFont(name);
        if (font == null)
            return new Font("Empty", new(), new((0, 0), Graphics.ImageType.Argb));
        return font;
    }
}
