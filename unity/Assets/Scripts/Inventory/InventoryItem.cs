namespace TooFishy
{
    [System.Serializable]
    public class InventoryItem
    {
        public FishType Type;
        public float Weight;
        public int Price;
        public int Id;
        public bool Shiny;

        public InventoryItem(FishType type, float weight, int price, int id, bool shiny)
        {
            Type = type;
            Weight = weight;
            Price = price;
            Id = id;
            Shiny = shiny;
        }
    }
}
