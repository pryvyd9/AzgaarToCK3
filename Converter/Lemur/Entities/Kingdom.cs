using ImageMagick;

namespace Converter.Lemur.Entities
{
    public class Kingdom : ITitle
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public MagickColor? Color { get; set; }
        public List<Cell> Cells { get; set; } = new List<Cell>();

        public List<Duchy> Duchies { get; set; } = new List<Duchy>();

        public ITitle? Parent { get; set; }

        public Kingdom(int id, string name, MagickColor? color, List<Duchy>? duchies)
        {
            Id = id;
            Name = name;

            if (duchies != null)
            {
                Duchies = duchies;
                Cells = GetAllCells();
                duchies.ForEach(duchy => duchy.Parent = this);
            }
        }

        public List<Cell> GetAllCells()
        {
            //if no duchies throw exception saying no duchies
            if (Duchies.Count == 0)
            {
                throw new ArgumentException($"Cannot get cells from a kingdom [{Name}] with no duchies.");
            }
            //Get cells by calling Get cells on each duchy object in Duchies

            List<Cell> cells = new List<Cell>();
            foreach (var duchy in Duchies)
            {
                cells.AddRange(duchy.GetAllCells());
            }
            return cells;
        }

        public MagickColor? GetColor()
        {
            return Color ?? Duchies.FirstOrDefault()?.GetColor();
        }

        public Culture GetDominantCulture(Map map)
        {
            //Get the most common culture among the duchies in this kingdom
            Dictionary<Culture, int> cultureCounts = new Dictionary<Culture, int>();
            foreach (var duchy in Duchies)
            {
                var dominantCulture = duchy.GetDominantCulture(map);
                if (cultureCounts.ContainsKey(dominantCulture))
                {
                    cultureCounts[dominantCulture]++;
                }
                else
                {
                    cultureCounts[dominantCulture] = 1;
                }
            }
            return cultureCounts.OrderByDescending(x => x.Value).First().Key;
        }

        public Religion GetDominantReligion(Map map)
        {
            //Get the most common religion among the duchies in this kingdom
            Dictionary<Religion, int> religionCounts = new Dictionary<Religion, int>();
            foreach (var duchy in Duchies)
            {
                var dominantReligion = duchy.GetDominantReligion(map);
                if (religionCounts.ContainsKey(dominantReligion))
                {
                    religionCounts[dominantReligion]++;
                }
                else
                {
                    religionCounts[dominantReligion] = 1;
                }
            }
            return religionCounts.OrderByDescending(x => x.Value).First().Key;
        }

        public Dictionary<ITitle, int> GetNeighbours()
        {
            Dictionary<ITitle, int> neighbouringKingdoms = new Dictionary<ITitle, int>();

            foreach (var duchy in Duchies)
            {
                var localNeighbours = duchy.GetNeighbours();
                foreach (var neighbour in localNeighbours.Keys)
                {
                    // Ignore duchies within the same kingdom
                    if (Duchies.Contains(neighbour))
                    {
                        continue;
                    }

                    // The neighbour is from another kingdom, so we update the count
                    var kingdom = neighbour.Parent; // Assuming each duchy has a parent kingdom
                    if (!neighbouringKingdoms.ContainsKey(kingdom))
                    {
                        neighbouringKingdoms[kingdom] = localNeighbours[neighbour];
                    }
                    else
                    {
                        neighbouringKingdoms[kingdom] += localNeighbours[neighbour];
                    }
                }
            }

            return neighbouringKingdoms;
        }
    }
}