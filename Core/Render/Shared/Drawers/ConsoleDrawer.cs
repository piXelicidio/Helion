using System.Drawing;
using Helion.Render.Commands;
using Helion.Render.Commands.Alignment;
using Helion.Render.Shared.Drawers.Helper;
using Helion.Util;
using Helion.Util.Extensions;
using Helion.Util.Geometry;
using Helion.Util.Time;

namespace Helion.Render.Shared.Drawers
{
    /// <summary>
    /// Performs console drawing by issuing rendering commands.
    /// </summary>
    public static class ConsoleDrawer
    {
        private const int ConsoleFontSize = 32;
        private const int BlackBarDividerHeight = 3;
        private const int CaretWidth = 2;
        private const int LeftEdgeOffset = 8;
        private const int InputToMessagePadding = 8;
        private const int BetweenMessagePadding = 3;
        private const long FlashSpanNanos = 500 * 1000L * 1000L;
        private const long HalfFlashSpanNanos = FlashSpanNanos / 2;
        private const float BackgroundAlpha = 0.95f;
        private const string ConsoleFontName = "Console";
        private static readonly Color BackgroundFade = Color.FromArgb(230, 0, 0, 0);
        private static readonly Color InputFlashColor = Color.FromArgb(0, 255, 0);

        public static void Draw(HelionConsole console, Dimension viewport, RenderCommands renderCommands)
        {
            DrawHelper helper = new(renderCommands);

            renderCommands.ClearDepth();

            DrawBackgroundImage(viewport, helper);
            DrawInput(console, viewport, helper, out int inputDrawTop);
            DrawMessages(console, viewport, helper, inputDrawTop);
        }

        private static bool IsCursorFlashTime() => Ticker.NanoTime() % FlashSpanNanos < HalfFlashSpanNanos;

        private static void DrawBackgroundImage(Dimension viewport, DrawHelper draw)
        {
            (int width, int height) = viewport;
            int halfHeight = viewport.Height / 2;

            // Draw the background, depending on what is available.
            if (draw.ImageExists("CONBACK"))
                draw.Image("CONBACK", 0, 0, width, height, color: BackgroundFade, alpha: BackgroundAlpha);
            else if (draw.ImageExists("TITLEPIC"))
                draw.Image("TITLEPIC", 0, 0, width, halfHeight, color: BackgroundFade, alpha: BackgroundAlpha);
            else
                draw.FillRect(0, 0, width, 3, Color.Gray);

            // Draw the divider.
            draw.FillRect(0, halfHeight - BlackBarDividerHeight, viewport.Width, 3, Color.Black);
        }

        private static void DrawInput(HelionConsole console, Dimension viewport, DrawHelper draw,
            out int inputDrawTop)
        {
            int offsetX = LeftEdgeOffset;
            int middleY = viewport.Height / 2;
            int baseY = middleY - BlackBarDividerHeight - 5;

            draw.Text(Color.Yellow, console.Input, ConsoleFontName, ConsoleFontSize, out Dimension drawArea,
                offsetX, baseY, textbox: Align.BottomLeft);

            inputDrawTop = baseY - drawArea.Height;
            offsetX += drawArea.Width;

            if (IsCursorFlashTime())
            {
                // We want to pad right of the last character, only if there
                // are characters to draw.
                int cursorX = console.Input.Empty() ? offsetX : offsetX + 2;
                int barHeight = ConsoleFontSize - 2;
                draw.FillRect(cursorX, inputDrawTop, CaretWidth, barHeight, InputFlashColor);
            }
        }

        private static void DrawMessages(HelionConsole console, Dimension viewport, DrawHelper draw, int inputDrawTop)
        {
            int topY = inputDrawTop - InputToMessagePadding;

            foreach (ConsoleMessage msg in console.Messages)
            {
                draw.Text(msg.Message, ConsoleFontName, ConsoleFontSize, out Dimension drawArea,
                    LeftEdgeOffset, topY, textbox: Align.BottomLeft);

                topY -= drawArea.Height + BetweenMessagePadding;
                if (topY < 0)
                    break;
            }
        }
    }
}