using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WebApplication1.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProductsController(
        AppDbContext db
    )
    {
        _db = db;
    }


    // GET /api/products
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var products = await _db.Products
            .OrderBy(p => p.ExpiryDate)
            .Select(p => new
            {
                id = p.Id,
                name = p.ProductName,
                expiry = p.ExpiryDate,
                confidence = p.Confidence,
                notes = p.Notes,
                createdAt = p.CreatedAtUtc,
                notifiedForMonth = p.NotifiedForMonth
            })
            .ToListAsync(ct);

        return Ok(products);
    }

    // DELETE /api/products/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var product = await _db.Products.FindAsync([id], ct);
        if (product == null)
            return NotFound();

        _db.Products.Remove(product);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }
}