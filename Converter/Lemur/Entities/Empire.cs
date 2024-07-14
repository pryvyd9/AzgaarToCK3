using ImageMagick;

namespace Converter.Lemur.Entities
{
    public class Empire : ITitle
    {

        public Empire(int id, string name, MagickColor? magickColor)
        {
            Id = id;
            Name = name;
            Color = magickColor;
        }

        public int Id { get; set; }

        public string Name { get; set; }
        public MagickColor? Color { get; set; }
        public List<Cell> Cells { get; set; } = new List<Cell>();
        public List<Kingdom> Kingdoms { get; set; } = new List<Kingdom>();
        public Culture Culture { get; set; }
        public Religion Religion { get; set; }

        public ITitle? Parent { get; set; }

        public List<Cell> GetAllCells()
        {
            //get all cells in kingdoms unless there are no kingdoms, then return cells
            if (Kingdoms.Count == 0)
            {
                return Cells;
            }
            else
            {
                List<Cell> cells = new List<Cell>();
                foreach (var kingdom in Kingdoms)
                {
                    cells.AddRange(kingdom.GetAllCells());
                }
                return cells;
            }
        }

        public MagickColor? GetColor()
        {
            //colour if not null if null use kingdoms
            return Color ?? Kingdoms.FirstOrDefault()?.GetColor();
        }

        public Culture GetDominantCulture(Map map)
        {
            //Accoring to the kingdoms in this empire, what is the most common culture?
            Dictionary<Culture, int> cultureCounts = new Dictionary<Culture, int>();
            foreach (var kingdom in Kingdoms)
            {
                var dominantCulture = kingdom.GetDominantCulture(map);
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
            //According to the kingdoms in this empire, what is the most common religion?
            Dictionary<Religion, int> religionCounts = new Dictionary<Religion, int>();
            foreach (var kingdom in Kingdoms)
            {
                var dominantReligion = kingdom.GetDominantReligion(map);
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
            Dictionary<ITitle, int> neighbouringEmpires = new Dictionary<ITitle, int>();
        
            foreach (var kingdom in Kingdoms)
            {
                var localNeighbours = kingdom.GetNeighbours();
                if (localNeighbours == null || !localNeighbours.Any())
                {
                    continue;
                }
                foreach (var neighbour in localNeighbours.Keys)
                {
                    // Ignore kingdoms within the same empire
                    if (Kingdoms.Contains(neighbour))
                    {
                        continue;
                    }
        
                    // The neighbour is from another empire, so we update the count
                    var empire = neighbour.Parent; // Assuming each kingdom has a parent empire
                    if (!neighbouringEmpires.ContainsKey(empire))
                    {
                        neighbouringEmpires[empire] = localNeighbours[neighbour];
                    }
                    else
                    {
                        neighbouringEmpires[empire] += localNeighbours[neighbour];
                    }
                }
            }
        
            return neighbouringEmpires;
        }
    }
}