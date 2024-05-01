using System;
using System.Linq;

namespace AzgaarToCK3;

public static class Helper
{
    public static bool IsCellMountains(int height)
    {
        return height > 500;
    }
    public static bool IsCellHills(int biomeId, int height)
    {
        if (height > 500)
        {
            return false;
        }
        return IsHills(biomeId, height);
    }
    private static bool IsHills(int biomeId, int heightDifference)
    {
        return heightDifference > 100 && biomeId is 4 or 5 or 6 or 7 or 8 or 10 or 11;
    }
    public static string? GetProvinceBiomeName(int biomeId, int heightDifference)
    {
        // plains/farmlands/hills/mountains/desert/desert_mountains/oasis/jungle/forest/taiga/wetlands/steppe/floodplains/drylands
        /*
         * 	0"Marine",
			1"Hot desert",
			2"Cold desert",
			3"Savanna",
			4"Grassland",
			5"Tropical seasonal forest",
			6"Temperate deciduous forest",
			7"Tropical rainforest",
			8"Temperate rainforest",
			9"Taiga",
			10"Tundra",
			11"Glacier",
			12"Wetland"
         * */
        if (heightDifference > 500)
        {
            return biomeId switch
            {
                0 => null,
                1 or 3 => "desert_mountains",
                _ => "mountains",
            };
        }
        else if (IsHills(biomeId, heightDifference))
        {
            return "hills";
        }
        return biomeId switch
        {
            0 => null, // Marine > ocean
            1 => "desert",// Hot desert > desert
            2 => "taiga",// Cold desert > taiga
            3 => "steppe",// Savanna > steppe
            4 => "plains",// Grassland > plains
            5 => "farmlands",// Tropical seasonal forest > farmlands
            6 => "forest",// Temperate deciduous forest > forest
            7 => "jungle",// Tropical rainforest > jungle
            8 => "forest",// "Temperate rainforest" > forest
            9 => "taiga",// Taiga > taiga
            10 => "taiga",// Tundra > taiga
            11 => "floodplains",// Glacier > floodplains
            12 => "wetlands",// Wetland > wetlands
            _ => throw new ArgumentException("Unrecognized biomeId")
        }; ;
    }

    //public static double Percentile(double[] sequence, double excelPercentile)
    //{
    //    Array.Sort(sequence);
    //    int N = sequence.Length;
    //    double n = (N - 1) * excelPercentile + 1;
    //    // Another method: double n = (N + 1) * excelPercentile;
    //    if (n == 1d) return sequence[0];
    //    else if (n == N) return sequence[N - 1];
    //    else
    //    {
    //        int k = (int)n;
    //        double d = n - k;
    //        return sequence[k - 1] + d * (sequence[k] - sequence[k - 1]);
    //    }
    //}
    public static double Percentile(int[] sequence, double excelPercentile)
    {
        Array.Sort(sequence);
        int N = sequence.Length;
        double n = (N - 1) * excelPercentile + 1;
        // Another method: double n = (N + 1) * excelPercentile;
        if (n == 1d) return sequence[0];
        else if (n == N) return sequence[N - 1];
        else
        {
            int k = (int)n;
            double d = n - k;
            return sequence[k - 1] + d * (sequence[k] - sequence[k - 1]);
        }
    }

    public static double HeightDifference(Province province)
    {
        var heights = province.Cells.Select(n => n.height).ToArray();
        return Percentile(heights, 0.7) - Percentile(heights, 0.3);
    }
}
