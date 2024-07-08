namespace Converter.Lemur
{
    using System.Diagnostics;
    using Converter.Lemur.Entities;
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
            GenerateCounties(map);
            AssignCellsToBaronies(map);
            

            AssignUniqueColorsToBaronies(map);
            await ImageUtility.DrawProvincesImage(map);



            Console.WriteLine("Finished conversion!");
        }

        private static void GenerateCounties(Map map)
        {
            //counties is where we will start to deviate from the Azgaar data model quite seriously as we will not have counties in the same way as in CK3
            //what we will instead do is group neighbouring baronies within the same duchy into counties.

            // We will do this like this:
            // find the larges barony in the duchy by population
            // Add the neighbouring barony of the lowest population to the county
        }

        private static void AssignUniqueColorsToBaronies(Map map)
        {
            for (int i = 0; i < map.Baronies!.Count; i++)
            {
                map.Baronies[i].Color = Helper.GetColor(i, map.Baronies.Count);
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


                //now each barony must bave a see cell, that is the cell that has the burg in it.
                foreach (Barony barony in baronies)
                {
                    barony.Cells.Add(barony.burg.Cell!);

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
                        //Then assign the closest cell to the burg
                        barony.Cells.Add(neighbors[0]);
                        //And remove it from the list of countryside cells
                        countrysideCells.Remove(neighbors[0]);
                    }
                    if (noMoreRoom)
                    {
                        Console.WriteLine("No more room in any barony");
                        break;
                    }
                }

                //if not all countryside cells are assigned, print a warning
                if (countrysideCells.Count > 0)
                {
                    Console.WriteLine($"Warning: {countrysideCells.Count} countryside cells are not assigned to any barony. Islands?");
                }

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
                //Skip the barony if it has ben marked as removed
                if (burg.Value.Removed)
                {
                    Console.WriteLine($"Skipping barony {burg.Value.Name} as it has been marked as removed");
                    continue;
                }
                //Warn the user if the barony is in wastelands (province 0 and state 0), since we will then skip it
                if (burg.Value.Cell!.AzProvince == 0 && burg.Value.Cell!.State == 0)
                {
                    Console.WriteLine($"Skipping barony {burg.Value.Name} as it is in wastelands");
                    continue;
                }


                baronies.Add(new Barony(burg.Value));

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
                    Console.WriteLine($"Skipping province {provinceData.name} as it has no burgs");
                    continue;
                }
                if (province.Key == 0)
                {
                    // TODO: Implement wastelands splitting if in a state
                    provinceData = new PackProvince(i: 0, name: "Wastelands", burg: 0, state: 0);

                    //TODO: get all cells in the province and group them by state
                    var WastelandCellsByState = province.GroupBy(c => c.Value.State).OrderBy(g => g.Key).OrderBy(g => g.Key);





                    //now for each state in the wastelands province, generate a duchy

                    foreach (var state in WastelandCellsByState)
                    {
                        // remove the first group as it is the wastelands province (Wastelands should not be a duchy)
                        // to find out if it is we must see if the first group is assigned to state 0
                        if (state.Key == 0)
                        {
                            continue;
                        }



                        //look up the state in the json data, we will reuse the state name as the duchy name
                        var stateData = map.JsonMap.pack.states.First(s => s.i == state.Key);
                        var d = new Duchy(stateData.i, state.Select(c => c.Value).ToList(), stateData.name);
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

            if (Settings.Instance.Debug)
            {
                foreach (var duchy in duchies)
                {
                    Console.WriteLine($"Duchy {duchy.Id} {duchy.Name} has {duchy.GetAllCells().Count} cells");
                }
            }
            Console.WriteLine($"Generated {duchies.Count} duchies");
        }




    }
}