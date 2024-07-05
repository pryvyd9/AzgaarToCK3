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



            // Form ck3 baronies


            Console.WriteLine("Finished conversion!");
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
                    // Neighbour is an array of 
                    Neighbors = cell.properties.neighbors,
                    Type = type,
                    GeoDataCoordinates = cell.geometry.coordinates[0],

                    // TODO: Assign burgs

                    //Biome = cell.properties.biome, comes form the other json

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




    }
}