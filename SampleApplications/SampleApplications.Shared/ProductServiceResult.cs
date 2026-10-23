namespace SampleApplications.Shared;

/// <summary>
/// Represents a completed simulated product-service response.
/// </summary>
/// <remarks>
/// Explicit status distinguishes successful empty responses from failures.
/// Successful results own a read-only snapshot of the returned products.
/// This type contains no logging, caching, or application-mode policy.
/// </remarks>
public sealed class ProductServiceResult
{
    private ProductServiceResult(ProductServiceStatus status, IReadOnlyList<ProductPrice> products)
    {
        Status = status;
        Products = products;
    }

    /// <summary>
    /// Gets the outcome reported by the simulated dependency.
    /// </summary>
    public ProductServiceStatus Status { get; }

    /// <summary>
    /// Gets whether the request completed successfully.
    /// </summary>
    public bool IsSuccess => Status == ProductServiceStatus.Success;

    /// <summary>
    /// Gets the returned products, or an empty collection for a failed result.
    /// </summary>
    /// <remarks>
    /// Check IsSuccess before interpreting an empty collection as a
    /// successful response with no products.
    /// </remarks>
    public IReadOnlyList<ProductPrice> Products { get; }

    /// <summary>
    /// Gets the immutable unavailable response.
    /// </summary>
    public static ProductServiceResult Unavailable { get; } = new(ProductServiceStatus.Unavailable, []);

    /// <summary>
    /// Gets the immutable authentication-rejection response.
    /// </summary>
    public static ProductServiceResult AuthenticationRejected { get; } = new(ProductServiceStatus.AuthenticationRejected, []);

    /// <summary>
    /// Gets the immutable authorization-denial response.
    /// </summary>
    public static ProductServiceResult AuthorizationDenied { get; } = new(ProductServiceStatus.AuthorizationDenied, []);

    /// <summary>
    /// Creates a successful response containing a snapshot of the products.
    /// </summary>
    /// <param name="products">The products returned by the service.</param>
    /// <returns>A successful, immutable response.</returns>
    /// <exception cref="ArgumentNullException">
    /// The products collection is null.
    /// </exception>
    public static ProductServiceResult Success(IEnumerable<ProductPrice> products)
    {
        ArgumentNullException.ThrowIfNull(products);

        return new ProductServiceResult(ProductServiceStatus.Success, Array.AsReadOnly(products.ToArray()));
    }
}