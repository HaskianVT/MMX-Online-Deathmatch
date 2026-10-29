using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MMXOnline
{
    public enum LeaveMatchScenario
    {
        LeftManually,
        MatchOver,
        ServerShutdown,
        Recreate,
        Rejoin,
        Kicked
    }

    public class LeaveMatchSignal
    {
        public LeaveMatchScenario leaveMatchScenario;
        public Server newServerData;
        public string kickReason;

        public LeaveMatchSignal(LeaveMatchScenario leaveMatchScenario, Server newServerData, string kickReason)
        {
            this.leaveMatchScenario = leaveMatchScenario;
            this.newServerData = newServerData;
            this.kickReason = kickReason;
        }

        public void createNewServer()
        {
            ConnectingMenu.run("Creating match", async () =>
            {
                // Wait for the old match to close on the relay, so the new one can use its name
                Stopwatch stopWatch = Stopwatch.StartNew();
                byte[] serverResponse = null;
                do
                {
                    serverResponse = await Global.matchmakingQuerier.sendAsync(newServerData.region.ip, "GetServer:" + newServerData.name, 1000);
                    if (stopWatch.ElapsedMilliseconds > 5000)
                    {
                        Menu.change(new ErrorMenu("Error: Could not create new server.", new MainMenu()));
                        return;
                    }
                }
                while (!serverResponse.IsNullOrEmpty() && await retryDelay());

                string errorMessage = await HostMenu.createServer(SelectCharacterMenu.playerData.charNum, newServerData, null, true, new MainMenu());

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    // Retry once
                    errorMessage = await HostMenu.createServer(SelectCharacterMenu.playerData.charNum, newServerData, null, true, new MainMenu());
                    if (!string.IsNullOrEmpty(errorMessage))
                    {
                        Menu.change(new ErrorMenu(errorMessage, new MainMenu()));
                    }
                }
            }, new MainMenu());
        }

        // Waits a bit between queries, so a request that fails right away doesn't spin without letting frames run. Always true.
        static async Task<bool> retryDelay()
        {
            await Task.Delay(100);
            return true;
        }

        public void rejoinNewServer()
        {
            ConnectingMenu.run("Joining match", async () =>
            {
                // Wait for the host to create the new match
                Stopwatch stopWatch = Stopwatch.StartNew();
                byte[] serverResponse;
                do
                {
                    serverResponse = await Global.matchmakingQuerier.sendAsync(newServerData.region.ip, "GetServer:" + newServerData.name, 1000);
                    if (stopWatch.ElapsedMilliseconds > 5000)
                    {
                        Menu.change(new ErrorMenu("Error: Could not rejoin new server.", new MainMenu()));
                        return;
                    }
                }
                while (serverResponse.IsNullOrEmpty() && await retryDelay());

                Server server = Helpers.deserialize<Server>(serverResponse);

                await JoinMenu.joinServer(server);
            }, new MainMenu());
        }
    }
}
