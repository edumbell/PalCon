using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Palcon.Models
{
    public struct Colour
    {

    }
    public class Game
    {
        public static List<Game> Games = new List<Game>();
        //public static Dictionary<string, string> Colours = new Dictionary<string, string>
        //{
        //    {"#bbb", "white" },{ "#55e079", "Green" },{   "#00eee0", "Azure" },{ "#ffd400", "Yellow" },{ "#aaaaff", "Blue" },{ "#ff7777", "Reddish" },{ "#ff00ff","purple"}
        //};
        public static string[] Colours =  {"#bbb", "#45e855",   "#00eec0", "#ffd400", "#95aaff", "#ff7777", "#ff00ff"};
        public static string[] ColourNames = { "white","Green","Azure","Yellow","Blue","Reddish","Purple" };
        public static int[] ColourIds = { 0,1,2,3,4,5,6};

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
            var usedColourIds = new List<int>();
            var usedColours = new List<string>();
            usedColourIds.Add(0); // neutral planets
            usedColours.Add(Colours[0]);
            var r = new Random();
            foreach (var p in Players.OrderBy(p => p.PlayerId))
            {
                if (usedColourIds.Contains(p.ColourId))
                {
                    var possibles = ColourIds.Where(x => !usedColourIds.Contains(x));
                    if (possibles.Any())
                    {
                        p.ColourId = possibles.OrderBy(x => r.Next(50)).First();
                    }
                }
                usedColourIds.Add(p.ColourId);
                p.Name = ColourNames[p.ColourId];
                p.Colour = Colours[p.ColourId];
                usedColours.Add(Colours[p.ColourId]);
            }
            return usedColours.ToArray();

        }

        public List<Player> LivePlayers()
        {
            return Players.Where(x => !x.IsDead).ToList();
        }

        public List<Player> LiveHumanPlayers()
        {
            return Players.Where(x => !x.IsDead && !x.IsAI).ToList();
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
        public int ColourId { get; set; }
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