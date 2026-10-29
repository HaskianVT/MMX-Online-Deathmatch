using System;
using System.Threading.Tasks;

namespace MMXOnline
{
    // Shown while waiting for the server (e.g. creating or joining a match), so the game loop keeps running and nothing else can be selected meanwhile
    public class ConnectingMenu : IMainMenu
    {
        string message;
        bool inGame;
        float time;

        public ConnectingMenu(string message, bool inGame = false)
        {
            this.message = message;
            this.inGame = inGame;
        }

        // Shows a ConnectingMenu while action runs. If it throws, shows the error and goes back to menuOnError.
        public static async void run(string message, Func<Task> action, IMainMenu menuOnError, bool inGame = false)
        {
            Menu.change(new ConnectingMenu(message, inGame));
            try
            {
                await action();
            }
            catch (Exception e)
            {
                Logger.logException(e, false);
                Menu.change(new ErrorMenu(new string[] { "Network error:", e.Message.Truncate(40) }, menuOnError, inGame));
            }
        }

        public void update()
        {
            time += Global.spf;
        }

        public void render()
        {
            float top = Global.screenH * 0.45f;

            if (!inGame) DrawWrappers.DrawTextureHUD(Global.textures["menubackground"], 0, 0);

            DrawWrappers.DrawRect(5, 5, Global.screenW - 5, Global.screenH - 5, true, new Color(0, 0, 0, 224), 0, ZIndex.HUD, false);

            string dots = new string('.', 1 + (int)(time * 3) % 3);
            Helpers.drawTextStd(message + dots, Global.screenW / 2, top, alignment: Alignment.Center, fontSize: 24);
        }
    }
}
