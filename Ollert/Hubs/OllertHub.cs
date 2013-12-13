﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.AspNet.SignalR;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;
using Newtonsoft.Json;
using Ollert.Models;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace Ollert.Hubs
{
    public class User
    {
        public string UserName { get; set; }
        public string UserId { get; set; }
        public HashSet<string> ConnectionIds { get; set; }
    }

    [Authorize]
    public class OllertHub : Hub
    {
        internal static readonly ConcurrentDictionary<string, User> ConnectedUsers = new ConcurrentDictionary<string, User>();
        public override Task OnConnected()
        {
            string userId = Guid.Empty.ToString();
            if (Context.User != null)
                userId = Context.User.Identity.GetUserId();

            string connectionId = Context.ConnectionId;

            var user = ConnectedUsers.GetOrAdd(userId, _ => new User
            {
                UserName = Context.User != null ? Context.User.Identity.Name : "<anonyme>",
                UserId = userId,
                ConnectionIds = new HashSet<string>()
            });

            lock (user.ConnectionIds)
            {
                user.ConnectionIds.Add(connectionId);
            }

            this.Clients.All.onConnected(ConnectedUsers.Select(u => u.Value));

            return base.OnConnected();
        }

        public override Task OnDisconnected()
        {
            // Retire l'id de la liste
            foreach (var connectedUser in ConnectedUsers)
            {
                lock (connectedUser.Value.ConnectionIds)
                {
                    connectedUser.Value.ConnectionIds = connectedUser.Value.ConnectionIds.Where(c => !c.Equals(Context.ConnectionId)).ToHashSet();
                }

                if (!connectedUser.Value.ConnectionIds.Any())
                {
                    User removedUser;
                    ConnectedUsers.TryRemove(connectedUser.Key, out removedUser);
                }
            }
            
            // Send to js client
            Clients.Others.onDisconnected(ConnectedUsers.Select(u => u.Value));

            return base.OnDisconnected();
        }
    }
}