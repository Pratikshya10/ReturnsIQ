using System;
using System.Collections.Generic;
using System.Text;

namespace ReturnsIQ.Core;

public record Product(string Sku, string Name, string Category, decimal Price);

public record Order(int OrderId, string CustomerEmail, DateTime OrderedAt);

public record OrderItem(int OrderId, string Sku, int Quantity);

public record ReturnRequest(
    int ReturnId, int OrderId, string Sku,
    string Reason, DateTime CreatedAt, string Status);

public record ReturnAnalysis(
    int ReturnId, string Category, string Sentiment, string RootCause,
    bool IsProductDefect, double Confidence, string Model, DateTime AnalyzedAt);

// Shape of the seed JSON file
public record SeedData(
    List<Product> Products, List<Order> Orders,
    List<OrderItem> OrderItems, List<ReturnRequest> Returns);
