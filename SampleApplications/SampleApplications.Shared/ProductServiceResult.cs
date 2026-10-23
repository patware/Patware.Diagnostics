namespace SampleApplications.Shared;

/// <summary>
/// Represents either a successful product response or service unavailability.
/// </summary>
/// <remarks>
/// A successful response can contain no products. IsAvailable distinguishes
/// that outcome from an unavailable service.
/// Successful results own a read-only snapshot of the supplied products,
/// preventing subsequent collection changes from altering cached results.
/// </remarks>
public sealed class ProductServiceResult
{
    private ProductServiceResult(bool isAvailable, IReadOnlyList<ProductPrice> products)
    {
        IsAvailable = isAvailable;
        Products = products;
    }

    /// <summary>
    /// Gets whether the service successfully fulfilled the request.
    /// </summary>
    public bool IsAvailable { get; }

    /// <summary>
    /// Gets the returned products, or an empty collection when unavailable.
    /// </summary>
    /// <remarks>
    /// Check IsAvailable before interpreting an empty collection as a
    /// successful response with no products.
    /// </remarks>
    public IReadOnlyList<ProductPrice> Products { get; }

    /// <summary>
    /// Gets the immutable unavailable result.
    /// </summary>
    public static ProductServiceResult Unavailable { get; } = new(false, Array.Empty<ProductPrice>());

    /// <summary>
    /// Creates a successful result containing a snapshot of the products.
    /// </summary>
    /// <param name="products">The products returned by the service.</param>
    /// <returns>A successful, immutable response.</returns>
    /// <exception cref="ArgumentNullException">
    /// The products collection is null.
    /// </exception>
    public static ProductServiceResult Success(IEnumerable<ProductPrice> products)
    {
        ArgumentNullException.ThrowIfNull(products);

        return new ProductServiceResult(true, Array.AsReadOnly(products.ToArray()));
    }
}