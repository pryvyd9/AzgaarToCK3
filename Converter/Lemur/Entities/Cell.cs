namespace Converter.Lemur.Entities
{

    /// <summary>
    /// The converters idea of a cell. Can hold various data from both the geojson and json files.
    /// </summary>
    public class Cell
    {

        public int Id { get; init; }
        public int Height { get; set; }
        public float[][] GeoDataCoordinates { get; set; }
        public int[] Neighbors { get; set; }
        public required int Culture { get; set; }
        public required int Religion { get; set; }
        public int Biome { get; set; }

        public FeatureType Type { get; set; }

        public IProvince Province { get; set; }


        //If the cell has a burg, this will be set.s
        public Burg? Burg { get; set; }
        public required int State { get; set; }

        public required int AzProvince { get; set; }

        public override string ToString()
        {
            return $"id:{Id},neighbors:[{string.Join(",", Neighbors)}]";
        }

        /// <summary>
        /// Use this when comparing distances between cells.
        /// Squared distance is faster to calculate than the actual distance, but gives the same relative results.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public float DistanceSquared(Cell other)
        {
            return (GeoDataCoordinates[0][0] - other.GeoDataCoordinates[0][0]) * (GeoDataCoordinates[0][0] - other.GeoDataCoordinates[0][0]) +
                (GeoDataCoordinates[0][1] - other.GeoDataCoordinates[0][1]) * (GeoDataCoordinates[0][1] - other.GeoDataCoordinates[0][1]);
        }


        /// <summary>
        /// Enum representing the type of geographical feature. 
        /// Includes both primary types (e.g., Ocean, Island, Lake) and subtypes (e.g., Continent, Isle, LakeIsland, Freshwater, Salt, Dry, Sinkhole, Lava).
        /// Source: https://github.com/Azgaar/Fantasy-Map-Generator/wiki/Data-model#pack-object-1
        /// </summary>
        public enum FeatureType
        {
            ocean,
            island,
            lake,
            continent,
            isle,
            lake_island,
            freshwater,
            salt,
            dry,
            sinkhole,
            lava
        }

    }
}