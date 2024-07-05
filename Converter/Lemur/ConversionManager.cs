namespace Converter.Lemur
{

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

            // Form ck3 baronies
            // TODO: FormBaronies(map);
            GenerateBaronies(map);
            AssignCellsToBaronies(map);



            Console.WriteLine("Finished conversion!");
        }



        private static void AssignCellsToBaronies(Map map)
        {
            //We willd o this by province
        }

        private static void LinkCellsToBurgs(Map map)
        {
            //For each burg (skip 0'eth) find the cell it is referenceing by cell id and assign it to the burg
            foreach (var burg in map.Burgs!.Skip(1))
            {
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
            Console.WriteLine("Generating baronies");
            // Next we instanciate a list of baronies. Since we know the final size of the list we can pre allocate the memory
            List<Barony> baronies = new(map.Burgs!.Count - 1);
            foreach (var burg in map.Burgs.Skip(1)) //0'eth entry is always empty (See Azgaar data model)
            {
                //Warn the user if the barony is in wastelands (province 0 and state 0), since we will then skip it
                if (burg.Value.Cell!.AzProvince == 0 && burg.Value.Cell!.State == 0)
                {
                    Console.WriteLine($"Skipping barony {burg.Value.Name} as it is in wastelands");
                    continue;
                }

                baronies.Add(new Barony(burg.Value));

            }
            map.Baronies = baronies;
            Console.WriteLine($"Generated {baronies.Count} baronies");
        }

        private static void GenerateDuchies(Map map)
        {
            //Duchies are based on Azgaar Provinces. Except in the case of the wastelands where parts of a state can be assigned to the wastelands province (0) and we must generate a new from the state.
            Console.WriteLine($"Generating duchies");
            //First we group the cells by province
            var cellsByProvince = map.Cells!.GroupBy(c => c.Value.AzProvince);

            List<Duchy> duchies = new(cellsByProvince.Count() + 10); // We allocate a bit more than we need to avoid resizing the list in case we end up splitting wastelands

            foreach (var province in cellsByProvince)
            {
                //look up the province in the json data
                var provinceData = map.JsonMap.pack.provinces.First(p => p.i == province.Key);
                if (province.Key == 0)
                {
                    // TODO: Implement wastelands splitting if in a state
                    provinceData = new PackProvince(i: 0, name: "Wastelands", burg: 0, state: 0);
                    //continue;
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


                List<Cell> cells = province.Select(c => c.Value).ToList();

                //Generate a duchy
                var duchy = new Duchy(provinceData.i, cells, provinceData.name);

                duchies.Add(duchy);

            }

            map.Duchies = duchies;
            Console.WriteLine($"Generated {duchies.Count} duchies");

            if (Settings.Instance.Debug)
            {
                foreach (var duchy in duchies)
                {
                    Console.WriteLine($"Duchy {duchy.Id} {duchy.Name} has {duchy.GetAllCells().Count} cells");
                }
            }
        }




    }
}