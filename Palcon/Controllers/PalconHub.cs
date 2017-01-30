using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.AspNet.SignalR;
using Palcon.Models;
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

                toJoin.Players.Add(player);
                foreach (var p in toJoin.Players)
                {
                    Clients.Client(p.ConnectionId).playerJoined(toJoin.Players.Count());
                }

                Clients.Client(player.ConnectionId).joinSuccess(toJoin.GameId);

            }
        }

        public void ClientReadyToStart(int gameId)
        {
            var game = Game.Games.Where(x => x.GameId == gameId).Single();
            var p = game.Players.Where(x => x.ConnectionId == Context.ConnectionId).Single();
            p.IsReadyToStart = true;

        }

        public void SendSettings(int gameId, string settings)
        {
            var game = Game.Games.Where(x => x.GameId == gameId).Single();
            if (!game.Started)
            {
                foreach (var p in game.Players)
                {
                    Clients.Client(p.ConnectionId).receiveSettings(settings);
                }
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
                    foreach (var p in game.Players)
                    {
                        Clients.Client(p.ConnectionId).checkReadyToStart();
                    }
                    System.Threading.Thread.Sleep(1000);
                    game.Players = game.Players.Where(x => x.IsReadyToStart).ToList();
                    return game.Players.Count();
                }
                else
                    return 0;
            }
        }

        public void SendMap(int gameId, string json)
        {
            var game = Game.Games.Where(x => x.GameId == gameId).Single();
            int pid = 0;
            foreach (var p in game.Players)
            {
                pid++;
                p.PlayerId = pid;
                Clients.Client(p.ConnectionId).receiveMap(p.PlayerId, json);
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

        public void SendCommands(int gameId, string json)
        {
            var game = Game.Games.Where(x => x.GameId == gameId).Single();
            var player = game.Players.Where(x => x.ConnectionId == Context.ConnectionId).Single();
            player.CurrentCommand = json;
            var allSent = game.AllCommandsIn();
            if (allSent)
            {
                EndTurn(game);
            }
            else
            {
                var schedule = System.Threading.Tasks.Task.Run(async () => {
                    await System.Threading.Tasks.Task.Delay(1500);
                    if (game.TimeLastTurnEnd.AddMilliseconds(1500) < DateTime.Now )
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