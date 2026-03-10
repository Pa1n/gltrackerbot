public class WatchItem
{
    public int SupplierId { get; set; }           // ID поставщика
    public List<int> ProductIds { get; set; } = new List<int>(); // Список товаров

    // Вместо bool — количество на прошлой проверке
    public List<int> LastStock { get; set; } = new List<int>();
}