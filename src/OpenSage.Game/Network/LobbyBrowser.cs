﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace OpenSage.Network
{
    public class LobbyBrowser
    {
        public struct LobbyPlayer
        {
            public string Name { get; set; }
        }

        public struct LobbyGame
        {
            public string Name { get; set; }
        }

        public Dictionary<IPEndPoint, LobbyGame> Games { get; }
        public Dictionary<IPEndPoint, LobbyPlayer> Players { get; }

        public string Username { get; set; }
        public IPAddress Self { get; set; }

        public bool Updated { get; set; }
        public bool InLobby { get; set; }
        public bool Hosting { get; set; }

        public LobbyBrowser()
        {
            Games = new Dictionary<IPEndPoint, LobbyGame>();
            Players = new Dictionary<IPEndPoint, LobbyPlayer>();
            Username = Environment.MachineName;
            InLobby = false;
            Hosting = false;
            Updated = true;
            Self = Dns.GetHostAddresses(Dns.GetHostName()).FirstOrDefault(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
        }
    }
}
