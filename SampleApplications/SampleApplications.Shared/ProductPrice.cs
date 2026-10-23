namespace SampleApplications.Shared;

/// <summary>
/// Represents a product price returned by the simulated dependency.
/// </summary>
/// <param name="Name">The product name.</param>
/// <param name="Price">The simulated price in CAD.</param>
public sealed record ProductPrice(string Name, decimal Price);