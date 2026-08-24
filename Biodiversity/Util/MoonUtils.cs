using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Biodiversity.Util
{
    public static class MoonUtils
    {
        private static TerminalKeyword RouteKW = null;

        /// <summary>
        /// Get the wanted moon name without a prefix, if there is any
        /// </summary>
        /// <param name="planetName">The wanted moon name to normalize</param>
        /// <returns>String representing the normalized moon name</returns>
        public static string GetNormalizedMoonName(string planetName)
        {
            string moonName = Regex.Replace(planetName, "^[0-9]+", string.Empty);
            if (moonName[0] == ' ' || moonName[0] == '-')
                moonName = moonName[1..];
            return moonName;
        }


        /// <summary>
        /// Finds and returns the wanted level's terminal routing price
        /// </summary>
        /// <param name="level">The level to get the price from</param>
        /// <returns>Integer corresponding to the routing price or null if the price was not found</returns>
        public static int? GetMoonRoutingPrice(SelectableLevel level)
        {
            if (RouteKW == null)
            {
                Terminal terminal = Object.FindObjectOfType<Terminal>();
                if (terminal == null)
                {
                    return null;
                }
                try
                {
                    RouteKW = terminal.terminalNodes.allKeywords.First(keyword => keyword.word == "route");
                    if (RouteKW == null)
                    {
                        return null;
                    }
                }
                catch (System.InvalidOperationException)
                {
                    return null;
                }
            }
            foreach (CompatibleNoun noun in RouteKW.compatibleNouns)
            {
                if (noun.result.displayPlanetInfo == level.levelID)
                {
                    return noun.result.itemCost;
                }
            }
            return null;
        }
    }
}
