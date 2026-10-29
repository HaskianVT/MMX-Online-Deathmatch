using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MMXOnline
{
    public class MatchmakingQuerier
    {
        public MatchmakingQuerier()
        {
        }

        public TcpClient getTcpClient(string ip, int timeoutMs)
        {
            var client = new TcpClient();
            var result = client.BeginConnect(ip, Global.basePort, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(timeoutMs));
            if (!success)
            {
                throw new Exception("Failed to connect.");
            }
            return client;
        }

        // Opens a matchmaking connection to the relay (TCP, or a WebSocket for "ws://"/"wss://" regions), runs exchange on it and returns its result.
        // Returns null if anything fails.
        private T sendExchange<T>(string ip, int? timeoutMs, Func<Stream, T> exchange) where T : class
        {
            try
            {
                if (timeoutMs == null) timeoutMs = Options.main.getNetworkTimeoutMs();
                if (WebSocketTunnel.isWebSocketUrl(ip))
                {
                    using Stream webSocketStream = WebSocketTunnel.openTcpStream(ip, timeoutMs.Value);
                    return exchange(webSocketStream);
                }
                using (TcpClient client = getTcpClient(ip, timeoutMs.Value))
                {
                    using NetworkStream networkStream = client.GetStream();
                    client.SendTimeout = timeoutMs.Value;
                    client.ReceiveTimeout = timeoutMs.Value;
                    return exchange(networkStream);
                }
            }
            catch
            {
                return null;
            }
        }

        private string sendHelper(string ip, string query, int? timeoutMs = null)
        {
            return sendExchange(ip, timeoutMs, stream =>
            {
                stream.SendStringMessage(query);
                return stream.ReadStringMessage();
            });
        }

        private byte[] sendHelperGetBytes(string ip, string query, int? timeoutMs = null)
        {
            return sendExchange(ip, timeoutMs, stream =>
            {
                stream.SendStringMessage(query);
                return stream.ReadMessage();
            });
        }

        public string send(string ip, string query, string responseHeader, int? timeoutMs = null)
        {
            string result = sendHelper(ip, query, timeoutMs);
            if (result == null) return null;

            if (result.StartsWith(responseHeader + ":"))
            {
                result = result.Substring(responseHeader.Length + 1);
            }

            return result;
        }

        public byte[] send(string ip, string query, int? timeoutMs = null)
        {
            return sendHelperGetBytes(ip, query, timeoutMs);
        }

        public CreateServerResponse createServer(Server serverData)
        {
            var requestServer = new Server(Global.version, serverData.region, serverData.name, serverData.level, serverData.shortLevelName, serverData.gameMode, serverData.playTo, serverData.botCount, serverData.maxPlayers, serverData.timeLimit ?? 0, serverData.fixedCamera, serverData.hidden,
                serverData.netcodeModel, serverData.netcodeModelPing, serverData.isLAN, serverData.mirrored, serverData.useLoadout, Global.checksum, LevelData.getChecksumFromName(serverData.level), LevelData.getCustomMapUrlFromName(serverData.level), 
                serverData.extraCpuCharData, serverData.customMatchSettings, serverData.disableHtSt, serverData.disableVehicles);

            var response = send(serverData.region.ip, "CreateServer:" + JsonConvert.SerializeObject(requestServer), "CreateServer");
            if (!string.IsNullOrEmpty(response))
            {
                if (response.StartsWith("fail:"))
                {
                    string failReason = "Error: " + response.RemovePrefix("fail:");
                    return new CreateServerResponse()
                    {
                        failReason = failReason
                    };
                }
                else
                {
                    return new CreateServerResponse()
                    {
                        server = JsonConvert.DeserializeObject<Server>(response)
                    };
                }
            }
            else
            {
                return new CreateServerResponse()
                {
                    failReason = "Error: could not connect to server."
                };
            }
        }

        /*
        public string joinServer(string serverName)
        {
            var response = send("JoinServer:" + serverName, "JoinServer");
            return response;
        }
        */
    }

    public class CreateServerResponse
    {
        public string failReason;
        public Server server;
    }
}
