using System.Text.Json;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public class FileRepository
{
  private readonly string _filePath = "items.json";

  public List<WatchItem> LoadData()
  {
    if (!File.Exists(_filePath))
      return new List<WatchItem>();
    var json = File.ReadAllText(_filePath);
    return JsonSerializer.Deserialize<List<WatchItem>>(json) ?? new List<WatchItem>();
  }

  public void SaveData(List<WatchItem> data)
  {
    var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(_filePath, json);
  }

  public void AddProduct(int supplierId, int productId)
  {
    var data = LoadData();
    var supplier = data.FirstOrDefault(x => x.SupplierId == supplierId);
    if (supplier == null)
    {
      supplier = new WatchItem { SupplierId = supplierId };
      data.Add(supplier);
    }
    if (!supplier.ProductIds.Contains(productId))
    {
      supplier.ProductIds.Add(productId);
      supplier.LastStock.Add(0);
    }
    SaveData(data);
  }

  public void DeleteProduct(int supplierId, int productId)
  {
    var data = LoadData();
    var supplier = data.FirstOrDefault(x => x.SupplierId == supplierId);
    if (supplier != null)
    {
      int index = supplier.ProductIds.IndexOf(productId);
      if (index >= 0)
      {
        supplier.ProductIds.RemoveAt(index);
        supplier.LastStock.RemoveAt(index);
        SaveData(data);
      }
    }
  }

  public List<int> GetProducts(int supplierId)
  {
    return LoadData().FirstOrDefault(x => x.SupplierId == supplierId)?.ProductIds ?? new List<int>();
  }

  public void DeleteSupplier(int supplierId)
  {
    var data = LoadData();
    data.RemoveAll(x => x.SupplierId == supplierId);
    SaveData(data);
  }
}