public class Supplier
{
    public int id { get; set; }           // ID поставщика
    public string name { get; set; }              // Название поставщика
    public string alias { get; set; }             // Короткий ключ
    public string city { get; set; }              // Город
    public string email { get; set; }             // Email, если есть
    public string note { get; set; }              // Дополнительно
    public string type { get; set; }              // Тип (Интернет-магазин и т.п.)
}