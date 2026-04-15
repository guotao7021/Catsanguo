using System.Collections.Generic;
using System.Linq;

namespace CatSanguo.WorldMap;

public static class MapPathfinder
{
    /// <summary>
    /// BFS shortest path on the city connection graph.
    /// Returns ordered list of city IDs from start to end (inclusive), or empty list if no path.
    /// When team is specified, garrisoned enemy passes block pathfinding.
    /// </summary>
    public static List<string> FindPath(string startCityId, string endCityId, List<CityNode> allCities, string? team = null)
    {
        if (startCityId == endCityId) return new List<string> { startCityId };

        var cityLookup = allCities.ToDictionary(c => c.Data.Id, c => c);
        if (!cityLookup.ContainsKey(startCityId) || !cityLookup.ContainsKey(endCityId))
            return new List<string>();

        // BFS
        var queue = new Queue<string>();
        var parent = new Dictionary<string, string>();
        var visited = new HashSet<string>();

        queue.Enqueue(startCityId);
        visited.Add(startCityId);

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();

            if (current == endCityId)
            {
                // Reconstruct path
                var path = new List<string>();
                string? node = endCityId;
                while (node != null)
                {
                    path.Add(node);
                    node = parent.ContainsKey(node) ? parent[node] : null;
                }
                path.Reverse();
                return path;
            }

            var city = cityLookup[current];
            if (city.Data.ConnectedCityIds == null) continue;

            foreach (var neighborId in city.Data.ConnectedCityIds)
            {
                if (!visited.Contains(neighborId) && cityLookup.ContainsKey(neighborId))
                {
                    // Pass blocking: garrisoned enemy passes block movement
                    if (team != null && neighborId != endCityId)
                    {
                        var nd = cityLookup[neighborId].Data;
                        if (nd.CityType == "pass" && nd.Garrison.Count > 0 && !IsFriendly(nd.Owner, team))
                            continue;
                    }

                    visited.Add(neighborId);
                    parent[neighborId] = current;
                    queue.Enqueue(neighborId);
                }
            }
        }

        return new List<string>(); // No path found
    }

    /// <summary>
    /// Check if owner is friendly to team.
    /// player↔player friendly, same enemy faction friendly, otherwise hostile.
    /// </summary>
    public static bool IsFriendly(string owner, string team)
    {
        if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(team)) return false;
        string o = owner.ToLower();
        string t = team.ToLower();
        if (o == t) return true;
        if (o == "player" && t == "player") return true;
        return false;
    }
}
