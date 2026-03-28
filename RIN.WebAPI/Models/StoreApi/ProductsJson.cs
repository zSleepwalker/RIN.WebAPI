namespace RIN.WebAPI.Models.StoreApi;

public class ProductsJson
{
    public Products[] products { get; set; } = System.Array.Empty<Products>();
    public int vip_time_remaining { get; set; }
}

public class Products
{
    public uint id { get; set; }
    public DateTime index_time { get; set; }
    public string sdb_type { get; set; } = null!;
    public uint sdb_id { get; set; }
    public uint duration { get; set; }
    public string name { get; set; } = null!;
    public string sdb_description { get; set; } = null!;
    public uint sex { get; set; }
    public bool permanent { get; set; }
    public bool active { get; set; }
    public uint category_id { get; set; }
    public uint category_parent_id { get; set; }
    public bool featured { get; set; }
    public string description { get; set; } = null!;
    public string instructions { get; set; } = null!;
    public string video { get; set; } = null!;
    public string preview_image { get; set; } = null!;
    public string overview_image { get; set; } = null!;
    public DateTime created_at { get; set; }
    public string name_en { get; set; } = null!;
    public Bundles[] bundles { get; set; } = System.Array.Empty<Bundles>();
    public bool approved { get; set; }
    public uint best_discount { get; set; }
    public uint lowest_price { get; set; }
    public string family { get; set; } = null!;
}

public class Bundles
{
    public uint product_id { get; set; }
    public uint quantity { get; set; }
    public Price[] prices { get; set; } = System.Array.Empty<Price>();
    public Price current_price { get; set; } = null!;
}

public class Price
{
    public uint id { get; set; }
    public uint discount { get; set; }
    public uint global_quantity { get; set; }
    public uint starts_at { get; set; }
    public uint expires_at { get; set; }
    public uint original_amount { get; set; }
    public uint amount { get; set; }
}