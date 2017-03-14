using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Palcon.Models
{
    public class Game
    {
        public static List<Game> Games = new List<Game>();
        public static string[] Colours =  {"#bbb", "#55e079",   "#00eee0", "#ffd400", "#aaaaff", "#ff7777", "#ff00ff"};

        public int GameId { get; set; }
        public List<Player> Players { get; set; }
        public bool Started { get; set; }
        public int msPerTurn = 500;
        public int turnId = 0;
        public DateTime TimeLastTurnEnd { get; set; }
        public Game()
        {
            Players = new List<Player>();
            TimeLastTurnEnd = DateTime.Now;
        }

        public string[] SetUniqueColours()
        {
            var usedColours = new List<string>();
            usedColours.Add(Colours[0]); // neutral planets
            foreach (var p in Players.OrderBy(p => p.PlayerId))
            {
                if (usedColours.Contains(p.Colour))
                {
                    var possibles = Colours.Where(x => !usedColours.Contains(x));
                    var r = new Random();
                    if (possibles.Any())
                    {
                        p.Colour = possibles.OrderBy(x => r.Next(50)).First();
                    }
                }
                usedColours.Add(p.Colour);
            }
            return usedColours.ToArray();

        }

        public List<Player> LivePlayers()
        {
            return Players.Where(x => !x.IsDead).ToList();
        }

        public List<Player> LiveHumanPlayers()
        {
            return Players.Where(x => !x.IsDead && ! x.IsAI).ToList();
        }

        public List<Player> HumanPlayers()
        {
            return Players.Where(x => !x.IsAI).ToList();
        }

        public bool AllCommandsIn()
        {
            var alivePlayerCount = LiveHumanPlayers().Count();
            var commandsSent = LiveHumanPlayers().Where(x => x.CurrentCommand != null).Count();
            return alivePlayerCount == commandsSent;
        }

        public string EndTurnAndGetCommands()
        {
            turnId++;
            TimeLastTurnEnd = DateTime.Now;
            string result = "";
            foreach (var p in LivePlayers())
            {
                if (p.CurrentCommand != null)
                {
                    if (p.LagScore > 0)
                        p.LagScore -= 1;
                    result += p.CurrentCommand.Trim('[').Trim(']') + ",";
                }
                else
                {
                    p.LagScore += 10;
                    if (p.LagScore > 100)
                        p.IsDead = true;
                }
                p.CurrentCommand = null;
            }
            result = result.Trim(',');
            return "{\"turnId\":" + turnId.ToString() + ",\"commands\": [" + result + "]}";
        }
    }

    public class Player
    {
        public int GameId { get; set; }
        public bool IsAI { get; set; }
        public int PlayerId { get; set; }
        public string Name { get; set; }
        public string Colour { get; set; }
        public bool IsDead { get; set; }
        public string ConnectionId { get; set; }
        public bool IsReadyToStart { get; set; }
        public string CurrentCommand { get; set; }
        public int LagScore { get; set; }
        public Player()
        {
            Colour = Game.Colours.First();
        }
    }
}