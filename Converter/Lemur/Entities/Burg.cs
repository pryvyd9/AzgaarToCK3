using System.Numerics;

namespace Converter.Lemur.Entities
{
    public class Burg(Converter.Burg burg)
    {
        public int id { get; set; } = burg.i;
        public string Name { get; set; } = burg.name;
        public Cell? Cell;

        public int Cell_id { get; set; } = burg.cell;
        public Vector2 Position { get; set; } = new Vector2(burg.x, burg.y);
        public int Culture { get; set; } = burg.culture;
        public int State { get; set; } = burg.state;
        public int Feature { get; set; } = burg.feature;
        public float Population { get; set; } = burg.population;
        public string Type { get; set; } = burg.type;
        public bool Capital { get; set; } = burg.capital == 1;
        public bool Port { get; set; } = burg.port == 1;
        public bool Citadel { get; set; } = burg.citadel == 1;
        public bool Plaza { get; set; } = burg.plaza == 1;
        public bool Shanty { get; set; } = burg.shanty == 1;
        public bool Temple { get; set; } = burg.temple == 1;
        public bool Walls { get; set; } = burg.walls == 1;
        public bool Removed { get; set; } = burg.removed;

        //To string
        public override string ToString()
        {
            return $"id:{id},name:{Name},cell_id:{Cell_id},position:{Position},culture:{Culture},state:{State},feature:{Feature},population:{Population},type:{Type},capital:{Capital},port:{Port},citadel:{Citadel},plaza:{Plaza},shanty:{Shanty},temple:{Temple},walls:{Walls},removed:{Removed}";
        }
    }
}