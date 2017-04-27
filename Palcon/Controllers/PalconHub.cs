using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.AspNet.SignalR;
using Palcon.Models;
using System.Threading.Tasks;
namespace Palcon.Controllers
{
    public class PalconHub : Hub
    {
        public static object _lock = new object();
        public void JoinGame()
        {
            lock (_lock)
            {
                var toJoin = Game.Games.Where(x => !x.Started).FirstOrDefault();
                if (toJoin == null)
                {
                    toJoin = new Game();

                    if (Game.Games.Any())
                    {
                        toJoin.GameId = Game.Games.Max(x => x.GameId) + 1;
                    }
                    else
                        toJoin.GameId = 1;
                    Game.Games.Add(toJoin);
                }
                var player = new Player();
                //if (toJoin.Players.Any())
                //{
                //    player.PlayerId = toJoin.Players.Max(x => x.PlayerId) + 1;
                //}
                //else
                //    player.PlayerId = 1;
                player.ConnectionId = Context.ConnectionId;
                player.GameId = toJoin.GameId;
                toJoin.Players.Add(player);
                foreach (var p in toJoin.HumanPlayers())
                {
                    Clients.Client(p.ConnectionId).playerJoined(toJoin.Players.Count());
                }

                Clients.Client(player.ConnectionId).joinSuccess(toJoin.GameId);

            }
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            var ps = Game.Games.SelectMany(x => x.Players).Where(x => x.ConnectionId == Context.ConnectionId).ToList();
            foreach (var player in ps)
            {
                var game = Game.Games.Where(x => x.GameId == player.GameId).Single();
                if (!game.Started)
                {
                    game.Players.Remove(player);

                    foreach (var p in game.HumanPlayers())
                    {
                        Clients.Client(p.ConnectionId).playerJoined(game.Players.Count());
                    }
                }
                else
                {
                    SendChat(game.GameId, player.PlayerId, null, "[has disconnected]");
                    player.IsDead = true;
                }

            }
            return base.OnDisconnected(stopCalled);
        }

        public void ClientReadyToStart(int gameId)
        {
            var game = Game.Games.Where(x => x.GameId == gameId).Single();
            var p = game.HumanPlayers().Where(x => x.ConnectionId == Context.ConnectionId).Single();
            p.IsReadyToStart = true;

        }

        public void SendSettings(int gameId, string settings)
        {
            var game = Game.Games.Where(x => x.GameId == gameId).Single();
            foreach (var p in game.HumanPlayers())
            {
                Clients.Client(p.ConnectionId).receiveSettings(settings);
            }

        }

        public int TryStartGame(int gameId)
        {
            lock (_lock)
            {
                var game = Game.Games.Where(x => x.GameId == gameId).Single();
                if (!game.Started)
                {
                    game.Started = true;
                    foreach (var p in game.LiveHumanPlayers())
                    {
                        Clients.Client(p.ConnectionId).checkReadyToStart();
                    }
                    System.Threading.Thread.Sleep(1500);
                    var initiatingPlayer = game.LiveHumanPlayers().Where(x => x.ConnectionId == Context.ConnectionId).Single();
                    initiatingPlayer.IsReadyToStart = true; // need to assume this, as websockets(?) doesn't allow two connections?
                    game.Players = game.Players.Where(x => x.IsReadyToStart || x.IsAI).ToList();
                    return game.Players.Count();
                }
                else
                    return 0;
            }
        }

        public void SendMap(int gameId, string json, int numAiPlayers)
        {
            var game = Game.Games.Where(x => x.GameId == gameId).Single();
            int pid = 0;
            foreach (var p in game.LiveHumanPlayers())
            {
                pid++;
                p.PlayerId = pid;
            }
            for (var a = 0; a < numAiPlayers; a++)
            {
                pid++;
                game.Players.Add(new Player()
                {
                    ConnectionId = "ai",
                    PlayerId = pid,
                    IsAI = true,
                    IsReadyToStart = true,
                });
            }

            var colours = game.SetUniqueColours();
            foreach (var p in game.LiveHumanPlayers())
            {
                Clients.Client(p.ConnectionId).receiveMap(p.PlayerId, json,
                    Newtonsoft.Json.JsonConvert.SerializeObject(colours));
            }
        }

        public void EndTurn(Game game)
        {
            var jsonAll = game.EndTurnAndGetCommands();
            foreach (var p in game.LivePlayers())
            {
                Clients.Client(p.ConnectionId).receiveCommands(jsonAll);
            }
        }

        public void SendColour(int gameId, int col)
        {
            var game = Game.Games.Where(x => x.GameId == gameId).Single();
            if (!game.Started)
            {
                var player = game.LiveHumanPlayers().Where(x => x.ConnectionId == Context.ConnectionId).Single();
                player.ColourId = col;
                player.Colour = Game.Colours[col];
            }
        }

        public void SendChat(int gameId, int? pid, int? toPid, string msg)
        {
            var game = Game.Games.Where(x => x.GameId == gameId).Single();
            Player player;
            if (pid.HasValue)
            {
                player = game.Players.Where(x => x.PlayerId == pid).Single();
            }
            else
            {
                // game not started yet - no AI's
                player = game.LiveHumanPlayers().Where(x => x.ConnectionId == Context.ConnectionId).Single();
            }
            string toname = "";
            if (HttpContext.Current != null)
            {
                msg = HttpContext.Current.Server.HtmlEncode(msg);
            }
            if (toPid.HasValue)
            {
                var player2 = game.Players.Where(x => x.PlayerId == toPid).Single();
                toname = "<span class='chatname' style=\"border-color:" + player2.Colour + "\">" + player2.Name + "</span>";
                msg = "<span class='aichat'>" + msg + "</span>";
            }
            //var player = game.pla().Where(x => x.PlayerId == pid).Single();

            foreach (var p in game.LiveHumanPlayers())
            {
                Clients.Client(p.ConnectionId).receiveChat(player.PlayerId, string.Format(msg, toname));
            }
        }

        public void SendCommands(int gameId, string json)
        {
            var game = Game.Games.Where(x => x.GameId == gameId).FirstOrDefault();
            if (game == null)
                return;
            var player = game.HumanPlayers().Where(x => x.ConnectionId == Context.ConnectionId).Single();
            player.CurrentCommand = json;
            var allSent = game.AllCommandsIn();
            if (allSent)
            {
                EndTurn(game);
            }
            else
            {
                var schedule = System.Threading.Tasks.Task.Run(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(1500);
                    if (game.TimeLastTurnEnd.AddMilliseconds(1500) < DateTime.Now)
                    {
                        EndTurn(game);
                    }
                });
            }
        }



        public void Hello()
        {
            Clients.All.hello();
        }
    }
}