namespace Converter.Lemur
{
    using System.Diagnostics;
    using System.Drawing;
    using System.Security.Cryptography.X509Certificates;
    using Converter.Lemur.Entities;
    using Converter.Lemur.Graphs;
    using ImageMagick;
    using static Converter.Lemur.Entities.Cell;

    public class ConversionManager
    {


        // Class members and methods go here

        public async static Task Run()
        {
            var map = await InitializeMapWithAzgaarData();
            //print the response to the console
            Console.WriteLine($"{map} has been loaded.");

            // Repack the data from the json and geojson files into a dictionary of cells
            RepackCellsFromDataAsDictionary(map);
            RepackBurgs(map);


            LinkCellsToBurgs(map);

            GenerateDuchies(map);
            GenerateBaronies(map);



            AssignCellsToBaronies(map);

            GenerateWastelandProvinces(map);
            AssertEveryLandCellIsAssignedToABurg(map);


            GenerateBaronyAdjacency(map);
            GenerateCounties(map);

            GenerateKingdoms(map);



            AssignUniqueColorsToBaronies(map); //Debugging
            await ImageUtility.DrawProvincesImage(map); //Debugging
            await ShowCounties(map);
            await ShowDuchies(map);
            await ShowKingdoms(map);



            Console.WriteLine("Finished conversion!");
        }



        private static void GenerateWastelandProvinces(Map map)
        {
            //For each province flagged as not having a burg, generate a wasteland province

        }
        private static void AssertEveryLandCellIsAssignedToABurg(Map map)
        {
            //Every cell should at this point either be assigned to a barony or be assigned to the wastelands
            var landCells = map.Cells!.Where(c => IsDryLand(c.Value.Type)).ToList();

            if (Settings.Instance.Debug)
            {
                Helper.PrintSectionHeader("Asserting that every land cell is assigned to a barony or wasteland");
                Console.WriteLine($"There are {landCells.Count} land cells");
            }

            bool listPassable = true;

            //Now run through every land cell and see if it is assigned to a barony or wasteland
            foreach (var cell in landCells)
            {
                var cellPassable = false;
                if (cell.Value.Province is Barony)
                {
                    cellPassable = true;
                }
                // Check if the cell is in one of the wastelands
                else if (cell.Value.Province is Wasteland)
                {
                    cellPassable = true;
                }
                if (!cellPassable)
                {
                    listPassable = false;
                    Console.WriteLine($"Failed: Cell {cell.Value.Id}, AzProvince {cell.Value.AzProvince}, State {cell.Value.State}");
                }
            }

            //If any cell is not assigned to a barony or wasteland, throw an exception
            if (!listPassable)
            {
                throw new Exception("Not all land cells are assigned to a barony or wasteland");
            }
            else if (Settings.Instance.Debug)
            {
                Console.WriteLine("All land cells are assigned to a barony or wasteland");
            }

        }

        private static void AssignUniqueColorsToBaronies(Map map)
        {
            //We start at 10 because we want to avoid black
            for (int i = 0; i < map.Baronies!.Count; i++)
            {
                map.Baronies[i].Color = Helper.GetColor(i + 10, map.Baronies.Count + 20); //pad it just in case
            }
        }
        private static void AssignUniqueColorsToCounties(Map map)
        {
            //We start at 10 because we want to avoid black
            for (int i = 0; i < map.Counties!.Count; i++)
            {
                map.Counties[i].Color = Helper.GetColor(i + 10, map.Counties.Count + 20); //pad it just in case
            }
        }

        private static void AssignCellsToBaronies(Map map)
        {

            Helper.PrintSectionHeader("Assigning cells to baronies");
            //We will do this by duchy
            foreach (Duchy duchy in map.Duchies!)
            {
                Console.WriteLine($"Duchy: {duchy.Name}");
                //We start by getting all the baronies in the duchy, we do this by getting all the burgs in the duchy and then getting the baronies from the burgs
                var baronies = duchy.GetAllCells().Select(c => c.Burg).Where(b => b != null).Select(b => b!.Barony).Distinct();
                //Sort them on population size decending baroniesInProvince[0].Burg.population
                baronies = baronies.OrderByDescending(b => b!.burg!.Population);

                //now each barony must bave a seed cell, that is the cell that has the burg in it.
                foreach (Barony barony in baronies)
                {
                    barony.Cells.Add(barony.burg.Cell!);
                    // Assign the barony to the cell
                    barony.burg.Cell!.Province = barony;

                    if (barony.burg.Cell == null) //should not be zero, i think but just in case
                    {
                        throw new Exception($"Burg {barony.burg.Name} has no cell");
                    }
                }


                // Now get all cells in the province that are not already assigned to a burg (country side cells)
                var countrysideCells = duchy.GetAllCells().Where(c => c.Burg == null).ToList();
                // Have each barony grow outwards until all countryside cells are assigned among the burgs based on distance
                while (countrysideCells.Count > 0)
                {
                    bool noMoreRoom = true;
                    foreach (Barony barony in baronies)
                    {
                        //Look at it's assigned cells
                        // And generate a list of neighbouring cells from the list of countryside cells
                        List<Cell> neighbors = countrysideCells.Where(c => barony.Cells.SelectMany(cell => cell.Neighbors).Distinct().Contains(c.Id)).ToList();
                        if (neighbors.Count == 0)
                        {
                            continue;
                        }
                        noMoreRoom = false;
                        //Then sort them by distance to the burg
                        neighbors.Sort((a, b) => a.DistanceSquared(barony.burg.Cell!).CompareTo(b.DistanceSquared(barony.burg.Cell!)));
                        //Then assign the closest cell to the barony
                        barony.Cells.Add(neighbors[0]);
                        // and assign the barony to the cell
                        neighbors[0].Province = barony;
                        //And remove it from the list of countryside cells
                        countrysideCells.Remove(neighbors[0]);

                    }
                    if (noMoreRoom)
                    {
                        //Check if there would be any cells left unassigned (there is room but it i across water)
                        if (countrysideCells.Count > 0)
                        {
                            //This places a "seed" on any island cell and respect distance. It will assign whole islands contigously to the same barony.
                            var shortestDistancePair = countrysideCells
                                .Select(island => new
                                {
                                    Island = island,
                                    ClosestBarony = baronies
                                        .Select(b => new { Barony = b, Distance = b.burg.Cell!.DistanceSquared(island) })
                                        .OrderBy(b => b.Distance)
                                        .First()
                                })
                                .OrderBy(pair => pair.ClosestBarony.Distance)
                                .First();

                            // shortestDistancePair contains the island and the closest barony with the shortest distance among all pairs
                            var island = shortestDistancePair.Island;
                            var closestBarony = shortestDistancePair.ClosestBarony.Barony;

                            // link the cell and the barony and remove the cell from the list of unassigned cells
                            closestBarony.Cells.Add(island);
                            island.Province = closestBarony;
                            countrysideCells.Remove(island);

                            continue;
                        }
                        else
                        {
                            break; //no more room in any barony and no more cells to assign
                        }
                    }


                }

                // //if not all countryside cells are assigned, print a warning

                if (Settings.Instance.Debug)
                {
                    //list baronies and the number of cells assigned to them
                    Console.WriteLine($"Duchy {duchy.Name} has {baronies.Count()} baronies");
                    foreach (var barony in baronies!)
                    {
                        Console.WriteLine($"{barony.Name} has {barony.Cells.Count} cells");
                    }
                }

                //print the response to the console
                Console.WriteLine("All cells assigned to baronies");
                //list baronies and the number of cells assigned to them

            }
        }

        private static void LinkCellsToBurgs(Map map)
        {
            Helper.PrintSectionHeader("Linking cells to burgs");

            //For each burg (skip 0'eth) find the cell it is referenceing by cell id and assign it to the burg
            foreach (var burg in map.Burgs!.Skip(1))
            {
                if (burg.Value.Removed)
                {
                    continue;
                }
                burg.Value.Cell = map.Cells![burg.Value.Cell_id];
                //and reverse
                map.Cells![burg.Value.Cell_id].Burg = burg.Value;

                Console.WriteLine($"Burg {burg.Value.Name} <<=>> {burg.Value.Cell_id} Cell");
            }
        }

        private static async Task<Map> InitializeMapWithAzgaarData()
        {
            // Load the source data
            var geoMap = await MapManager.LoadGeojson();
            var jsonMap = await MapManager.LoadJson();


            var map = new Map
            {
                GeoMap = geoMap,
                JsonMap = jsonMap,
                Settings = Settings.Instance
            };


            return map;
        }

        /// <summary>
        /// Repack data from json and geojson into a dictionary of cells
        /// </summary>
        /// <param name="map"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private static void RepackCellsFromDataAsDictionary(Map map)
        {
            Dictionary<int, Cell> cells = [];

            var cellData = map.GeoMap.features; //Each feature is the representation for a single cell
                                                // Each cell has a list of properties and a list for geometry
                                                // The list of properties contains the id, heigh, neighbours e.c.t

            foreach (var cell in cellData)
            {
                if (!Enum.TryParse(cell.properties.type, true, out FeatureType type))
                {
                    // Throw an exception if the type is not recognized
                    throw new Exception($"Unrecognized feature type: {cell.properties.type}");
                }


                cells.Add(cell.properties.id, new Cell()
                {
                    Id = cell.properties.id,
                    Height = cell.properties.height,
                    Culture = cell.properties.culture,
                    Religion = cell.properties.religion,
                    State = cell.properties.state,
                    AzProvince = cell.properties.province,
                    // Neighbour is an array of cell ids
                    Neighbors = cell.properties.neighbors,
                    Type = type,
                    GeoDataCoordinates = cell.geometry.coordinates[0],

                    // TODO: BIOME

                    //Province = map.Provinces.First(p => p.Id == cell.properties.province),

                    //TODO: Json data has much of the same data - but also some details that are unique, like information about roads. 
                    //Right now there is no need for that data as far as I can see, but it might be useful later

                });

            }

            Console.WriteLine($"Repacked {cells.Count} cells from data");
            map.Cells = cells;
        }
        private static void RepackBurgs(Map map)
        {
            // Repack the burgs as a dictionary with the burg.id as the key for easy lookup
            var burgs = map.JsonMap.pack.burgs.ToDictionary(
                burg => burg.i,
                burg => new Burg(burg));
            map.Burgs = burgs;
        }

        /// <summary>
        /// Generate a list of baronies with the bare minimum of information
        /// </summary>
        /// <param name="burgs">Burgs to base the baronies on</param>
        /// <returns>A list of baronies</returns>
        private static void GenerateBaronies(Map map)
        {
            Helper.PrintSectionHeader("Generating baronies");
            // Next we instanciate a list of baronies. Since we know the final size of the list we can pre allocate the memory
            List<Barony> baronies = new(map.Burgs!.Count - 1);
            foreach (var burg in map.Burgs.Skip(1)) //0'eth entry is always empty (See Azgaar data model)
            {
                if (burg.Value.Removed)
                {
                    Console.WriteLine($"Skipping barony {burg.Value.Name} as it has been marked as removed");
                    continue;
                }
                if (burg.Value.Cell!.AzProvince == 0 && burg.Value.Cell!.State == 0)
                {
                    Console.WriteLine($"Skipping barony {burg.Value.Name} as it is in wastelands");
                    continue;
                }

                var barony = new Barony(burg.Value);
                barony.burg.Cell!.Duchy!.Baronies.Add(barony);
                // if the burg is a province capital, flagg it as such
                baronies.Add(barony);


            }
            map.Baronies = baronies;

            if (Settings.Instance.Debug)
            {
                foreach (var barony in baronies)
                {
                    Console.WriteLine($"Barony {barony.Id} {barony.Name}");
                }
            }

            Console.WriteLine($"Generated {baronies.Count} baronies");
        }

        private static void GenerateDuchies(Map map)
        {
            //Duchies are based on Azgaar Provinces. Except in the case of the wastelands where parts of a state can be assigned to the wastelands province (0) and we must generate a new from the state.
            Helper.PrintSectionHeader("Generating duchies");
            //First we group the cells by province
            var cellsByProvince = map.Cells!.GroupBy(c => c.Value.AzProvince).OrderBy(g => g.Key);

            List<Duchy> duchies = new(cellsByProvince.Count() + 10); // We allocate a bit more than we need to avoid resizing the list in case we end up splitting wastelands

            foreach (var province in cellsByProvince)
            {
                //look up the province in the json data
                var provinceData = map.JsonMap.pack.provinces.First(p => p.i == province.Key);

                //sanity check that the first cell in the province matches the province data
                if (province.First().Value.AzProvince != province.Key)
                {
                    throw new Exception($"Province {province.Key} does not match the first cell in the province: {province.First().Value.AzProvince}");
                }

                //if Province has no cells, skip it
                if (!province.Any())
                {
                    //log the name of the province skipped
                    Console.WriteLine($"Skipping province {provinceData.name} as it has no cells");
                    continue;
                }

                //If none of the cells has a burg, skip the province
                if (!province.Any(c => c.Value.Burg != null))
                {
                    //log the name of the province skipped
                    Console.WriteLine($"Skipping province {provinceData.name} as it has no burgs (Wasteland)");

                    //We have also dicovered a wasteland province, so we generate a wasteland province and add it to the wastelands list
                    List<Cell> wastelandCells = province.Select(c => c.Value).ToList();
                    map.Wastelands?.Add(new Wasteland(province.Key, wastelandCells, provinceData.name));

                    continue;
                }
                if (province.Key == 0)
                {
                    // Handle the wastelands province, it might actually contain cells assigned to states
                    provinceData = new PackProvince(i: 0, name: "Wastelands", burg: 0, state: 0);
                    var WastelandCellsByState = province.GroupBy(c => c.Value.State).OrderBy(g => g.Key).OrderBy(g => g.Key);
                    //now for each state in the wastelands province, generate a duchy

                    foreach (var state in WastelandCellsByState)
                    {
                        // Handle the first group because that is actually the wastelands province
                        if (state.Key == 0)
                        {
                            map.Wastelands?.Add(new Wasteland(0, state.Select(c => c.Value).ToList(), "Wastelands"));
                            continue;
                        }

                        //look up the state in the json data, we will reuse the state name as the duchy name
                        var stateData = map.JsonMap.pack.states.First(s => s.i == state.Key);
                        var d = new Duchy(i: stateData.i, cells: state.Select(c => c.Value).ToList(), stateData.name);
                        duchies.Add(d);

                    }
                    Console.WriteLine($"Some cells in the wastelands province are assigned to states, generated {duchies.Count} duchies");
                    continue;
                }


                List<Cell> cells = province.Select(c => c.Value).ToList();

                //Generate a duchy
                var duchy = new Duchy(provinceData.i, cells, provinceData.name);

                duchies.Add(duchy);

            }

            map.Duchies = duchies;

            // Assign duchy to each cell directly
            foreach (var duchy in duchies)
            {
                foreach (var cell in duchy.GetAllCells())
                {
                    cell.Duchy = duchy;
                }
            }

            if (Settings.Instance.Debug)
            {
                foreach (var duchy in duchies)
                {
                    Console.WriteLine($"Duchy {duchy.Id} {duchy.Name} has {duchy.GetAllCells().Count} cells");
                }
            }
            Console.WriteLine($"Generated {duchies.Count} duchies");
        }

        private static void GenerateCounties(Map map)
        {
            Helper.PrintSectionHeader("Generating counties");
            //For each duchy, generate a graph of the duchy
            foreach (var duchy in map.Duchies!)
            {
                //Create a graph of the duchy
                Graph graph = new(null!);
                List<BaronyNode> baronyNodes = new();

                //for each barony create a BaronyNode and populate it with the barony and the node
                foreach (var barony in duchy.Baronies)
                {
                    Node node = new() { Name = barony.Name, Population = (int)barony.burg.Population };
                    baronyNodes.Add(new BaronyNode(node, barony));

                    // add the node to the graph
                    graph.AddNode(node);
                }

                //Add the edges to the graph. This is done by looking at the adjacencies of the baronies
                foreach (var baronyNode in baronyNodes)
                {
                    //Resolve what baronies this barony is adjacent to
                    var adjacentBaronies = baronyNode.Barony.Neighbors;

                    //if the adjacent baronies is null, then this barony has no adjacent baronies
                    if (adjacentBaronies == null)
                    {
                        continue;
                    }
                    //then work out what nodes these baronies are represented by
                    var adjacentNodes = baronyNodes.Where(bn => adjacentBaronies.Contains(bn.Barony)).Select(bn => bn.Node).ToList();

                    //add the edge to the graph
                    graph.AddEdge(baronyNode.Node, adjacentNodes);
                }
                //partition the graph into connected components
                var partitions = Graph.PartitionGraph(graph);

                //Each partition is a county, so nearly there. First we translate back from graphs to baronies
                var counties = new List<County>();
                foreach (var partition in partitions)
                {
                    //work out what baronies are in this partition from the BaronyNode
                    var baroniesInPartition = baronyNodes.Where(bn => partition.adjacencyList.ContainsKey(bn.Node)).Select(bn => bn.Barony).ToList();
                    //Create a county from the baronies:

                    //We do this by:
                    // 1. Determine the name of the county.
                    string name = baroniesInPartition.Where(b => b.burg.Capital).Select(b => b.Name).FirstOrDefault()!;
                    if (name == null || name == "")
                    {
                        name = baroniesInPartition.OrderByDescending(b => b.burg.Population).First().Name;
                    }

                    var county = new County(IdManager.Instance.GetNextId(), name, baronies: baroniesInPartition, duchy: duchy, capital: baroniesInPartition.First());
                    if (Settings.Instance.Debug)
                    {
                        Console.WriteLine($"County {county.Name} has {baroniesInPartition.Count} baronies");
                    }
                    counties.Add(county);
                }

                //add the counties to the map's list of counties
                map.Counties ??= new();
                map.Counties.AddRange(counties);
                Console.WriteLine($"Duchy {duchy.Name} has {counties.Count} counties");
            }
            Console.WriteLine($"Generated {map.Counties!.Count} counties");


        }

        private static void GenerateKingdoms(Map map)
        {
            //(Not implemented) A kingdom is a state, but there will be an option to merge small kingdoms into neighbors within the same empire. A small kingdom is defined as having X or fewer duchies, where X is configurable in the settings.
            // tODDO later
            // Add a setting for turning off small kingdoms
            // Add a setting for small kingdom threshold


            // Get all the duchies, order them by state, get the state data from the json data and create a kingdom from the state data
            // The kingdom will be named after the state
            Helper.PrintSectionHeader("Generating kingdoms");
            var duchiesByState = map.Duchies!.GroupBy(d => d.GetAllCells().First().State).OrderBy(g => g.Key);
            List<Kingdom> kingdoms = new(duchiesByState.Count()); // preallocate memory for the kingdoms

            foreach (var state in duchiesByState)
            {
                // Get the state data from the json data
                var stateData = map.JsonMap.pack.states.First(s => s.i == state.Key);
                var kingdom = new Kingdom(stateData.i, stateData.name, null)
                {
                    Duchies = state.ToList()
                };
                kingdoms.Add(kingdom);
            }

            // Assign the kingdoms to the map
            map.Kingdoms = kingdoms;

            if (Settings.Instance.Debug)
            {
                foreach (var kingdom in kingdoms)
                {
                    Console.WriteLine($"Kingdom {kingdom.Id} {kingdom.Name} has {kingdom.Duchies.Count} duchies");
                }
            }


        }
        struct BaronyNode(Node node, Barony barony)
        {
            public Node Node { get; } = node;
            public Barony Barony { get; } = barony;
        }

        /// <summary>
        /// Debugging method to show the counties on the map
        /// </summary>
        /// <param name="map"></param>
        /// <returns></returns>
        private static async Task ShowCounties(Map map)
        {
            Dictionary<MagickColor, List<Cell>> countyCellsByColour = new();
            foreach (var county in map.Counties!)
            {
                countyCellsByColour.Add(county.GetColor(), county.GetAllCells());
            }
            await ImageUtility.DrawCellsWithColourImage(countyCellsByColour, map, "counties", Color.Transparent); //Debugging
        }

        private static async Task ShowDuchies(Map map)
        {
            // Like with Counties, but for Duchies
            Dictionary<MagickColor, List<Cell>> duchyCellsByColour = new();
            foreach (var duchy in map.Duchies!)
            {
                duchyCellsByColour.Add(duchy.GetColor(), duchy.GetAllCells());
            }
            await ImageUtility.DrawCellsWithColourImage(duchyCellsByColour, map, "duchies", Color.Transparent); //Debugging
        }
        private static async Task ShowKingdoms(Map map)
        {
           // Liek for Duchies and Counties, but for Kingdoms
            Dictionary<MagickColor, List<Cell>> kingdomCellsByColour = new();
            foreach (var kingdom in map.Kingdoms!)
            {
                kingdomCellsByColour.Add(kingdom.GetColor(), kingdom.GetAllCells());
            }
            await ImageUtility.DrawCellsWithColourImage(kingdomCellsByColour, map, "kingdoms", Color.Transparent); //Debugging
        }


        private static void GenerateBaronyAdjacency(Map map)
        {
            Helper.PrintSectionHeader("Generating barony adjacency");

            foreach (var barony in map.Baronies!)
            {
                Console.WriteLine($"Barony {barony.Name}");
                //fist get all the cells that the cells in this barony are adjacent to
                var cells = barony.GetAllCells();
                var adjacentCells = cells.SelectMany(c => c.Neighbors).Distinct().Select(k => map.Cells![k]).ToList();
                // Remove any cell that is in this barony
                adjacentCells.RemoveAll(cells.Contains);

                // Now find all unique baronies that the adjacent cells are in
                var adjacentBaronies = adjacentCells.Select(c => c.Province as Barony).Where(b => b != null).Distinct().ToList();

                if (Settings.Instance.Debug)
                {
                    Console.WriteLine($"Barony {barony.Name} has {adjacentBaronies.Count} adjacent baronies");
                }
                //Add the found baronies to this barony¨s list of adjacent baronies. Can be null if there are no adjacent baronies
                barony.Neighbors = adjacentBaronies!;
            }
        }
    }
}