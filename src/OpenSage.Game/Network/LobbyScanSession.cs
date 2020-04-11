﻿using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ProtoBuf;

namespace OpenSage.Network
{
    public class LobbyScanSession
    {
        private UdpClient _client;
        private IPEndPoint _receiveEp;
        private CancellationTokenSource _cancelTokenSource;
        private CancellationToken _cancelToken;
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private Game _game;

        public class LobbyScannedEventArgs : EventArgs
        {
            public IPEndPoint Host { get; set; }
            public string Name { get; set; }
            public string Map { get; set; }
        }

        public delegate void LobbyScannedEventHandler(Object sender, LobbyScannedEventArgs e);
        public event LobbyScannedEventHandler LobbyDetected;

        public LobbyScanSession(Game game)
        {
            _receiveEp = new IPEndPoint(IPAddress.Any, Ports.LobbyScan);
            _client = new UdpClient();
            _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _client.ExclusiveAddressUse = false;
            _game = game;
        }

        public async void Start()
        {
            _client.Client.Bind(_receiveEp);
            _cancelTokenSource = new CancellationTokenSource();
            _cancelToken = _cancelTokenSource.Token;

            await Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        var result = await _client.ReceiveAsync().WithCancellation(_cancelToken);
                        ProcessReceive(result);
                    }
                    catch (OperationCanceledException)
                    {
                        //Task is done / was canceld
                        break;
                    }
                }
            });
        }

        public void Stop()
        {
            _cancelTokenSource.Cancel();
            _client.Close();

        }

        private void ProcessReceive(UdpReceiveResult result)
        {
            // Check if is localhost
            if (IPAddress.IsLoopback(result.RemoteEndPoint.Address) ||
                result.RemoteEndPoint.Address.Equals(_game.LobbyBrowser.Self))
            {
                logger.Info($"Skipping: Received broadcast from localhost");
                return;
            }

            byte[] receiveBytes = result.Buffer;
            string ascii = Encoding.UTF32.GetString(receiveBytes);
            using (var receiveStream = new MemoryStream(receiveBytes))
            {
                // Deserialize response
                var response = Serializer.Deserialize<LobbyProtocol.LobbyBroadcast>(receiveStream);
                logger.Info($"Received broadcast from: {response.Name}");

                if (response.Host)
                {
                    // Add the game to our lobby
                    var lobbyGame = new LobbyBrowser.LobbyGame();
                    lobbyGame.Name = response.Name;

                    if (!_game.LobbyBrowser.Games.ContainsKey(result.RemoteEndPoint))
                    {
                        _game.LobbyBrowser.Games.Add(result.RemoteEndPoint, lobbyGame);
                    }

                    // Fire event
                    var args = new LobbyScannedEventArgs();
                    args.Host = result.RemoteEndPoint;
                    args.Name = response.Name;
                    LobbyDetected?.Invoke(this, args);
                }
                else if (!response.InLobby)
                {
                    // Add the game to our lobby
                    var lobbyPlayer = new LobbyBrowser.LobbyPlayer();
                    lobbyPlayer.Name = response.Name;

                    if (!_game.LobbyBrowser.Players.ContainsKey(result.RemoteEndPoint))
                    {
                        _game.LobbyBrowser.Players.Add(result.RemoteEndPoint, lobbyPlayer);
                    }
                }
            }
        }
    }
}
